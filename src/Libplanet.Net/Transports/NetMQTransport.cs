using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncIO;
using Dasync.Collections;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;
using NetMQ.Sockets;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using Serilog;

namespace Libplanet.Net.Transports;

/// <summary>
/// Implementation of <see cref="ITransport"/> interface using NetMQ.
/// </summary>
public class NetMQTransport : ITransport
{
    private readonly PrivateKey _privateKey;
    private readonly ProtocolOptions _protocolOptions;
    private readonly HostOptions _hostOptions;
    private readonly NetMQMessageCodec _messageCodec;
    private readonly Channel<MessageRequest> _requests;
    private readonly Task _runtimeProcessor;

    private NetMQQueue<(AsyncManualResetEvent, MessageEnvelope)>? _replyQueue;

    private RouterSocket? _router;
    private NetMQPoller? _routerPoller;
    private DnsEndPoint? _hostEndPoint;

    private CancellationTokenSource _runtimeCancellationTokenSource;
    private CancellationTokenSource _turnCancellationTokenSource;

    // Used only for logging.
    private long _requestCount;
    private long _socketCount;
    private bool _disposed = false;

    static NetMQTransport()
    {
        NetMQConfig.ThreadPoolSize = 3;
        ForceDotNet.Force();
    }

    private NetMQTransport(PrivateKey privateKey, ProtocolOptions protocolOptions, HostOptions hostOptions)
    {
        _socketCount = 0;
        _privateKey = privateKey;
        _hostOptions = hostOptions;
        _protocolOptions = protocolOptions;
        _messageCodec = new NetMQMessageCodec();

        _requests = Channel.CreateUnbounded<MessageRequest>();
        _runtimeCancellationTokenSource = new CancellationTokenSource();
        _turnCancellationTokenSource = new CancellationTokenSource();
        _requestCount = 0;
        CancellationToken runtimeCt = _runtimeCancellationTokenSource.Token;
        _runtimeProcessor = Task.Factory.StartNew(
            () =>
            {
                // Ignore NetMQ related exceptions during NetMQRuntime.Dispose() to stabilize
                // tests
                try
                {
                    using var runtime = new NetMQRuntime();
                    runtime.Run(ProcessRuntimeAsync(runtimeCt));
                }
                catch (Exception e)
                    when (e is NetMQException || e is ObjectDisposedException)
                {
                    // log
                }
            },
            runtimeCt,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        ProcessMessageHandler = new AsyncDelegate<MessageEnvelope>();
    }

    public AsyncDelegate<MessageEnvelope> ProcessMessageHandler { get; }

    public Peer Peer => new Peer { Address = _privateKey.Address, EndPoint = _hostEndPoint! };

    public DateTimeOffset? LastMessageTimestamp { get; private set; }

    public bool IsRunning => _routerPoller?.IsRunning ?? false;

    public Protocol Protocol => _protocolOptions.Protocol;

    public static async Task<NetMQTransport> Create(
        PrivateKey privateKey, ProtocolOptions protocolOptions, HostOptions hostOptions)
    {
        var transport = new NetMQTransport(privateKey, protocolOptions, hostOptions);
        transport.Initialize();
        return transport;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            throw new InvalidOperationException("Transport is already running.");
        }

        _runtimeCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _turnCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _replyQueue = new NetMQQueue<(AsyncManualResetEvent, MessageEnvelope)>();
        _routerPoller = new NetMQPoller { _router!, _replyQueue };

        _router!.ReceiveReady += ReceiveMessage!;
        _replyQueue.ReceiveReady += DoReply;

        Task pollerTask = RunPoller(_routerPoller!);
        new Task(async () =>
        {
            while (!_routerPoller.IsRunning)
            {
                await Task.Yield();
            }

        }).Start(_routerPoller);

        await pollerTask.ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQTransport));
        }

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        _replyQueue!.ReceiveReady -= DoReply!;
        _router!.ReceiveReady -= ReceiveMessage!;

        if (_routerPoller!.IsRunning)
        {
            _routerPoller.Stop();
        }

        _replyQueue.Dispose();

        await _runtimeCancellationTokenSource.CancelAsync();
    }

    public void Dispose()
    {
        if (IsRunning)
        {
            StopAsync(default).WaitWithoutException();
        }

        if (!_disposed)
        {
            _requests.Writer.TryComplete();
            _runtimeCancellationTokenSource.Cancel();
            _turnCancellationTokenSource.Cancel();
            _runtimeProcessor.WaitWithoutException();

            _runtimeCancellationTokenSource.Dispose();
            _turnCancellationTokenSource.Dispose();

            if (_router is { } router && !router.IsDisposed)
            {
                // We omitted _router.Unbind() with intention due to hangs.
                // See also: https://github.com/planetarium/libplanet/pull/2311
                _router.Dispose();
            }

            _routerPoller?.Dispose();

            _disposed = true;
        }
    }

    public async Task<MessageEnvelope> SendMessageAsync(
        Peer peer, IMessage message, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        IEnumerable<MessageEnvelope> replies =
            await SendMessageAsync(
                peer,
                message,
                timeout,
                1,
                false,
                cancellationToken).ConfigureAwait(false);
        MessageEnvelope reply = replies.First();

        return reply;
    }

    public async Task<IEnumerable<MessageEnvelope>> SendMessageAsync(
        Peer peer,
        IMessage message,
        TimeSpan? timeout,
        int expectedResponses,
        bool returnWhenTimeout,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var timerCts = new CancellationTokenSource();
        if (timeout is { } timeoutNotNull)
        {
            timerCts.CancelAfter(timeoutNotNull);
        }

        using CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                _runtimeCancellationTokenSource.Token,
                cancellationToken,
                timerCts.Token);
        CancellationToken linkedCt = linkedCts.Token;

        var replyList = new List<MessageEnvelope>();
        var channel = Channel.CreateUnbounded<NetMQMessage>();

        try
        {
            var request = new MessageRequest
            {
                SocketId = Guid.NewGuid(),
                MessageEnvelope = new MessageEnvelope
                {
                    Id = Guid.NewGuid(),
                    Message = message,
                    Protocol = _protocolOptions.Protocol,
                    Remote = Peer,
                    Timestamp = DateTimeOffset.UtcNow,
                },
                Peer = peer,
                ExpectedResponses = expectedResponses,
                Channel = channel,
                CancellationToken = linkedCt,
            };
            Interlocked.Increment(ref _requestCount);
            await _requests.Writer.WriteAsync(request, linkedCt).ConfigureAwait(false);

            foreach (var i in Enumerable.Range(0, expectedResponses))
            {
                NetMQMessage raw = await channel.Reader.ReadAsync(linkedCt).ConfigureAwait(false);
                MessageEnvelope reply = _messageCodec.Decode(raw);

                try
                {
                    reply.Validate(_protocolOptions.Protocol, _protocolOptions.MessageLifetime);
                }
                catch (InvalidOperationException dapve)
                {
                    channel.Writer.Complete(dapve);
                }

                replyList.Add(reply);
            }

            return replyList;
        }
        catch (OperationCanceledException oce) when (timerCts.IsCancellationRequested)
        {
            if (returnWhenTimeout)
            {
                return replyList;
            }

            throw WrapCommunicationException(
                new TimeoutException(
                    $"The operation was canceled due to timeout {timeout!.ToString()}.",
                    oce),
                peer,
                message);
        }
        catch (OperationCanceledException oce2)
        {
            const string dbgMsg =
                "{MethodName}() was cancelled while waiting for a reply to " +
                "{Content} {RequestId} from {Peer}";

            throw new TaskCanceledException(dbgMsg, oce2);
        }
        catch (ChannelClosedException ce)
        {
            throw WrapCommunicationException(ce.InnerException ?? ce, peer, message);
        }
        finally
        {
            channel.Writer.TryComplete();
        }
    }

    public void BroadcastMessage(IEnumerable<Peer> peers, IMessage content)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQTransport));
        }

        CancellationToken ct = _runtimeCancellationTokenSource.Token;
        List<Peer> boundPeers = peers.ToList();
        Task.Run(
            async () =>
            {
                // using Activity? a = _activitySource
                //     .StartActivity(ActivityKind.Producer)?
                //     .AddTag("Message", content.Type)
                //     .AddTag("Peers", boundPeers.Select(x => x.ToString()));
                await boundPeers.ParallelForEachAsync(
                    peer => SendMessageAsync(peer, content, TimeSpan.FromSeconds(1), ct),
                    ct);

                // a?.SetStatus(ActivityStatusCode.Ok);
            },
            ct);

    }

    public async Task ReplyMessageAsync(IMessage message, Guid id, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var ev = new AsyncManualResetEvent();
        _replyQueue!.Enqueue(
            (
                ev,
                new MessageEnvelope
                {
                    Id = id,
                    Message = message,
                    Protocol = _protocolOptions.Protocol,
                    Remote = Peer,
                    Timestamp = DateTimeOffset.UtcNow,
                }));

        await ev.WaitAsync(cancellationToken);
    }

    private void Initialize()
    {
        _router = new RouterSocket();
        _router.Options.RouterHandover = true;
        int listenPort = 0;

        if (_hostOptions.Port == 0)
        {
            listenPort = _router.BindRandomPort("tcp://*");
        }
        else
        {
            listenPort = _hostOptions.Port;
            _router.Bind($"tcp://*:{listenPort}");
        }

        if (_hostOptions.Host is { } host)
        {
            _hostEndPoint = new DnsEndPoint(host, listenPort);
        }
    }

    private void ReceiveMessage(object? sender, NetMQSocketEventArgs e)
    {
        try
        {
            var raw = new NetMQMessage();

            // execution limit to avoid starvation.
            for (var i = 0; i < 1_000; i++)
            {
                if (!e.Socket.TryReceiveMultipartMessage(TimeSpan.Zero, ref raw))
                {
                    break;
                }

                if (_runtimeCancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                LastMessageTimestamp = DateTimeOffset.UtcNow;

                // Duplicate received message before distributing.
                var copied = new NetMQMessage(raw.Select(f => f.Duplicate()));

                Task.Factory.StartNew(
                    async () =>
                    {
                        MessageEnvelope message = _messageCodec.Decode(copied);
                        string reqId = copied[0].Buffer.Length == 16 ?
                            new Guid(copied[0].ToByteArray()).ToString() : "unknown";

                        try
                        {
                            message.Validate(_protocolOptions.Protocol, _protocolOptions.MessageLifetime);
                            await ProcessMessageHandler.InvokeAsync(message);
                        }
                        catch (InvalidOperationException dapve)
                        {
                            const string logMsg =
                                "Received Request {RequestId} {Content} " +
                                "from {Peer} has an invalid APV {Apv}";

                            var diffVersion = new DifferentVersionMessage();

                            await ReplyMessageAsync(
                                diffVersion,
                                Guid.NewGuid(),
                                _runtimeCancellationTokenSource.Token);
                        }
                    },
                    CancellationToken.None,
                    TaskCreationOptions.HideScheduler | TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default)
                .Unwrap();
            }
        }
        catch (Exception ex)
        {
            // log
        }
    }

    private void DoReply(object? sender, NetMQQueueEventArgs<(AsyncManualResetEvent, MessageEnvelope)> e)
    {
        (AsyncManualResetEvent ev, MessageEnvelope message) = e.Queue.Dequeue();

        // FIXME The current timeout value(1 sec) is arbitrary.
        // We should make this configurable or fix it to an unneeded structure.
        NetMQMessage netMQMessage = _messageCodec.Encode(message, _privateKey);
        if (_router!.TrySendMultipartMessage(TimeSpan.FromSeconds(1), netMQMessage))
        {
        }
        else
        {
        }

        ev.Set();
    }

    private async Task ProcessRuntimeAsync(CancellationToken cancellationToken)
    {
        var reader = _requests.Reader;
        var synchronizationContext = SynchronizationContext.Current;
        await foreach (var request in reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _requestCount);
            _ = synchronizationContext.PostAsync(
                () => ProcessRequestAsync(request, request.CancellationToken));
        }
    }

    private async Task ProcessRequestAsync(MessageRequest request, CancellationToken cancellationToken)
    {
        var channel = request.Channel;
        var writer = channel.Writer;
        long? incrementedSocketCount = null;

        try
        {
            var peer = request.Peer;
            var address = await peer.ResolveNetMQAddressAsync(cancellationToken);
            using var socket = new DealerSocket();
            socket.Options.DisableTimeWait = true;
            socket.Options.Identity = request.SocketId.ToByteArray();
            socket.Connect(address);
            incrementedSocketCount = Interlocked.Increment(ref _socketCount);

            var netMQMessage = _messageCodec.Encode(request.MessageEnvelope, _privateKey);
            if (!socket.TrySendMultipartMessage(netMQMessage))
            {
                throw new InvalidOperationException();
            }

            for (var i = 0; i < request.ExpectedResponses; i++)
            {
                NetMQMessage raw = await socket.ReceiveMultipartMessageAsync(
                    cancellationToken: cancellationToken);

                await writer.WriteAsync(raw, cancellationToken);
            }

            writer.Complete();
        }
        catch (Exception e)
        {
            writer.TryComplete(e);
        }
        finally
        {
            if (request.ExpectedResponses == 0)
            {
                await TaskUtility.TryDelay(1000, default);
            }

            if (incrementedSocketCount is { })
            {
                Interlocked.Decrement(ref _socketCount);
            }
        }
    }

    private async Task RunPoller(NetMQPoller poller)
    {
        TaskCreationOptions taskCreationOptions =
            TaskCreationOptions.DenyChildAttach |
            TaskCreationOptions.LongRunning |
            TaskCreationOptions.HideScheduler;
        await Task.Factory.StartNew(
            () =>
            {
                // Ignore NetMQ related exceptions during NetMQPoller.Run() to stabilize
                // tests.
                try
                {
                    poller.Run();
                }
                catch (TerminatingException)
                {
                    // log
                }
                catch (ObjectDisposedException)
                {
                    // log
                }
                catch (Exception e)
                {
                    // log
                }
            },
            CancellationToken.None,
            taskCreationOptions,
            TaskScheduler.Default);
    }

    private InvalidOperationException WrapCommunicationException(
        Exception innerException,
        Peer peer,
        IMessage message)
    {
        return new InvalidOperationException(
            $"Failed to send and receive replies from {peer} for request {message}.");
    }

    private sealed record class MessageRequest
    {
        public required Guid SocketId { get; init; }

        public required MessageEnvelope MessageEnvelope { get; init; }

        public required Peer Peer { get; init; }

        public required int ExpectedResponses { get; init; }

        public required Channel<NetMQMessage> Channel { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }
}
