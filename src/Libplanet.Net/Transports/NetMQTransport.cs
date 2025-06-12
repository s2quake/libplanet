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
    private readonly MessageValidator _messageValidator;
    private readonly NetMQMessageCodec _messageCodec;
    private readonly Channel<MessageRequest> _requests;
    private readonly Task _runtimeProcessor;
    private readonly AsyncManualResetEvent _runningEvent;
    private readonly ActivitySource _activitySource;

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

    private NetMQTransport(
        PrivateKey privateKey,
        ProtocolOptions appProtocolVersionOptions,
        HostOptions hostOptions,
        TimeSpan? messageTimestampBuffer = null)
    {
        _socketCount = 0;
        _privateKey = privateKey;
        _hostOptions = hostOptions;
        _protocolOptions = appProtocolVersionOptions;
        _messageValidator = new MessageValidator(
            _protocolOptions, messageTimestampBuffer);
        _messageCodec = new NetMQMessageCodec();

        _requests = Channel.CreateUnbounded<MessageRequest>();
        _runtimeCancellationTokenSource = new CancellationTokenSource();
        _turnCancellationTokenSource = new CancellationTokenSource();
        _activitySource = new ActivitySource("Libplanet.Net.Transports.NetMQTransport");
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
                    runtime.Run(ProcessRuntime(runtimeCt));
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

        _runningEvent = new AsyncManualResetEvent();
        ProcessMessageHandler = new AsyncDelegate<MessageEnvelope>();
    }

    public AsyncDelegate<MessageEnvelope> ProcessMessageHandler { get; }

    public Peer AsPeer => new Peer { Address = _privateKey.Address, EndPoint = _hostEndPoint! };

    public DateTimeOffset? LastMessageTimestamp { get; private set; }

    public bool Running => _routerPoller?.IsRunning ?? false;

    public Protocol Protocol => _protocolOptions.Protocol;

    public ImmutableSortedSet<Address> AllowedSigners => _protocolOptions.AllowedSigners;

    public DifferentAppProtocolVersionEncountered DifferentAppProtocolVersionEncountered =>
    _protocolOptions.DifferentAppProtocolVersionEncountered;

    public static async Task<NetMQTransport> Create(
        PrivateKey privateKey,
        ProtocolOptions appProtocolVersionOptions,
        HostOptions hostOptions,
        TimeSpan? messageTimestampBuffer = null)
    {
        var transport = new NetMQTransport(
            privateKey,
            appProtocolVersionOptions,
            hostOptions,
            messageTimestampBuffer);
        await transport.Initialize();
        return transport;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQTransport));
        }

        if (Running)
        {
            throw new TransportException("Transport is already running.");
        }

        _runtimeCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _turnCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _replyQueue = new NetMQQueue<(AsyncManualResetEvent, MessageEnvelope)>();
        _routerPoller = new NetMQPoller { _router!, _replyQueue };

        _router!.ReceiveReady += ReceiveMessage!;
        _replyQueue.ReceiveReady += DoReply!;

        Task pollerTask = RunPoller(_routerPoller!);
        new Task(async () =>
        {
            while (!_routerPoller.IsRunning)
            {
                await Task.Yield();
            }

            _runningEvent.Set();
        }).Start(_routerPoller);

        await pollerTask.ConfigureAwait(false);
    }

    public async Task StopAsync(
    TimeSpan waitFor,
    CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQTransport));
        }

        if (Running)
        {
            await Task.Delay(waitFor, cancellationToken);

            _replyQueue!.ReceiveReady -= DoReply!;
            _router!.ReceiveReady -= ReceiveMessage!;

            if (_routerPoller!.IsRunning)
            {
                _routerPoller.Stop();
            }

            _replyQueue.Dispose();

            _runtimeCancellationTokenSource.Cancel();
            _runningEvent.Reset();
        }
    }

    public void Dispose()
    {
        if (Running)
        {
            StopAsync(TimeSpan.Zero).WaitWithoutException();
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

    public Task WaitForRunningAsync() => _runningEvent.WaitAsync();

    public async Task<MessageEnvelope> SendMessageAsync(
        Peer peer,
        IMessage content,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        IEnumerable<MessageEnvelope> replies =
            await SendMessageAsync(
                peer,
                content,
                timeout,
                1,
                false,
                cancellationToken).ConfigureAwait(false);
        MessageEnvelope reply = replies.First();

        return reply;
    }

    public async Task<IEnumerable<MessageEnvelope>> SendMessageAsync(
        Peer peer,
        IMessage content,
        TimeSpan? timeout,
        int expectedResponses,
        bool returnWhenTimeout,
        CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQTransport));
        }

        // using Activity? a = _activitySource
        //     .StartActivity(ActivityKind.Producer)?
        //     // .AddTag("Message", content.Type)
        //     .AddTag("Peer", peer.ToString());

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

        Guid reqId = Guid.NewGuid();
        var replies = new List<MessageEnvelope>();

        Channel<NetMQMessage> channel = Channel.CreateUnbounded<NetMQMessage>();

        try
        {
            var req = new MessageRequest(
                reqId,
                new MessageEnvelope
                {
                    Message = content,
                    Protocol = _protocolOptions.Protocol,
                    Remote = AsPeer,
                    Timestamp = DateTimeOffset.UtcNow,
                },
                peer,
                expectedResponses,
                channel,
                linkedCt);
            Interlocked.Increment(ref _requestCount);
            await _requests.Writer.WriteAsync(
                req,
                linkedCt).ConfigureAwait(false);


            foreach (var i in Enumerable.Range(0, expectedResponses))
            {
                NetMQMessage raw = await channel.Reader
                    .ReadAsync(linkedCt)
                    .ConfigureAwait(false);
                MessageEnvelope reply = _messageCodec.Decode(raw, true);

                try
                {
                    _messageValidator.ValidateTimestamp(reply);
                    _messageValidator.ValidateProtocol(reply);
                }
                catch (InvalidMessageTimestampException imte)
                {
                    channel.Writer.Complete(imte);
                }
                catch (InvalidProtocolException dapve)
                {
                    channel.Writer.Complete(dapve);
                }

                replies.Add(reply);
            }

            // a?.SetStatus(ActivityStatusCode.Ok);
            return replies;
        }
        catch (OperationCanceledException oce) when (timerCts.IsCancellationRequested)
        {
            if (returnWhenTimeout)
            {
                return replies;
            }

            // a?.SetStatus(ActivityStatusCode.Error);
            // a?.AddTag("Exception", nameof(TimeoutException));
            throw WrapCommunicationFailException(
                new TimeoutException(
                    $"The operation was canceled due to timeout {timeout!.ToString()}.",
                    oce),
                peer,
                content);
        }
        catch (OperationCanceledException oce2)
        {
            const string dbgMsg =
                "{MethodName}() was cancelled while waiting for a reply to " +
                "{Content} {RequestId} from {Peer}";

            // a?.SetStatus(ActivityStatusCode.Error);
            // a?.AddTag("Exception", nameof(TaskCanceledException));

            // Wrapping to match the previous behavior of `SendMessageAsync()`.
            throw new TaskCanceledException(dbgMsg, oce2);
        }
        catch (ChannelClosedException ce)
        {
            // a?.SetStatus(ActivityStatusCode.Error);
            // a?.AddTag("Exception", nameof(ChannelClosedException));
            throw WrapCommunicationFailException(ce.InnerException ?? ce, peer, content);
        }
        catch (Exception e)
        {
            const string errMsg =
                "{MethodName}() encountered an unexpected exception while waiting for " +
                "a reply to {Content} {RequestId} from {Peer}";
            // _logger.Error(
            //     e, errMsg, nameof(SendMessageAsync), content, reqId, peer.Address);
            // a?.SetStatus(ActivityStatusCode.Error);
            // a?.AddTag("Exception", e.GetType().ToString());
            throw;
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

    public async Task ReplyMessageAsync(
    IMessage content,
    byte[] identity,
    CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQTransport));
        }

        string reqId = !(identity is null) && identity.Length == 16
            ? new Guid(identity).ToString()
            : "unknown";

        var ev = new AsyncManualResetEvent();
        _replyQueue!.Enqueue(
            (
                ev,
                new MessageEnvelope
                {
                    Message = content,
                    Protocol = _protocolOptions.Protocol,
                    Remote = AsPeer,
                    Timestamp = DateTimeOffset.UtcNow,
                }));

        await ev.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Initializes a <see cref="NetMQTransport"/> as to make it ready to
    /// send request <see cref="MessageBase"/>s and receive reply <see cref="MessageEnvelope"/>s.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to propagate a notification
    /// that this operation should be canceled.</param>
    /// <returns>An awaitable <see cref="Task"/> without value.</returns>
    private async Task Initialize(CancellationToken cancellationToken = default)
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

        await Task.CompletedTask;
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
                        try
                        {
                            MessageEnvelope message = _messageCodec.Decode(
                                copied,
                                false);
                            string reqId = copied[0].Buffer.Length == 16 ?
                                new Guid(copied[0].ToByteArray()).ToString() : "unknown";
                            
                            try
                            {
                                _messageValidator.ValidateTimestamp(message);
                                _messageValidator.ValidateProtocol(message);
                                await ProcessMessageHandler.InvokeAsync(message);
                            }
                            catch (InvalidMessageTimestampException imte)
                            {
                                const string logMsg =
                                    "Received {RequestId} {Content} from " +
                                    "{Peer} has an invalid timestamp {Timestamp}";

                            }
                            catch (InvalidProtocolException dapve)
                            {
                                const string logMsg =
                                    "Received Request {RequestId} {Content} " +
                                    "from {Peer} has an invalid APV {Apv}";

                                var diffVersion = new DifferentVersionMessage();

                                await ReplyMessageAsync(
                                    diffVersion,
                                    message.Identity ?? Array.Empty<byte>(),
                                    _runtimeCancellationTokenSource.Token);
                            }
                        }
                        catch (InvalidMessageContentException ex)
                        {
                            // log
                        }
                        catch (Exception exc)
                        {
                            // log
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

    private void DoReply(
        object? sender,
        NetMQQueueEventArgs<(AsyncManualResetEvent, MessageEnvelope)> e)
    {
        (AsyncManualResetEvent ev, MessageEnvelope message) = e.Queue.Dequeue();
        string reqId = message.Identity is { } identity && identity.Length == 16
            ? new Guid(identity).ToString()
            : "unknown";

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

    private async Task ProcessRuntime(CancellationToken cancellationToken)
    {
        const string waitMsg = "Waiting for a new request...";
        ChannelReader<MessageRequest> reader = _requests.Reader;
#if NETCOREAPP3_0 || NETCOREAPP3_1 || NET
        await foreach (MessageRequest req in reader.ReadAllAsync(cancellationToken))
        {
#else
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.Verbose(waitMsg);
            MessageRequest req = await reader.ReadAsync(cancellationToken);
#endif
            long left = Interlocked.Decrement(ref _requestCount);

            _ = SynchronizationContext.Current.PostAsync(
                () => ProcessRequest(req, req.CancellationToken));

#if NETCOREAPP3_0 || NETCOREAPP3_1 || NET
#endif
        }
    }

    private async Task ProcessRequest(MessageRequest req, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        Channel<NetMQMessage> channel = req.Channel;

        int receivedCount = 0;
        long? incrementedSocketCount = null;

        // Normal OperationCanceledException initiated from outside should bubble up.
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var dealer = new DealerSocket();
            dealer.Options.DisableTimeWait = true;
            dealer.Options.Identity = req.Id.ToByteArray();
            try
            {
                dealer.Connect(await req.Peer.ResolveNetMQAddressAsync());
                incrementedSocketCount = Interlocked.Increment(ref _socketCount);
            }
            catch (NetMQException nme)
            {
                const string logMsg =
                    "{SocketCount} sockets open for processing requests; " +
                    "failed to create an additional socket for request {Message} {RequestId}";
                throw;
            }

            var netMQMessage = _messageCodec.Encode(
                req.Message,
                _privateKey);
            if (dealer.TrySendMultipartMessage(netMQMessage))
            {
                // log
            }
            else
            {
                throw new SendMessageFailException(
                    $"Failed to send {req.Message.Message} to {req.Peer}.",
                    req.Peer);
            }

            foreach (var i in Enumerable.Range(0, req.ExpectedResponses))
            {
                NetMQMessage raw = await dealer.ReceiveMultipartMessageAsync(
                    cancellationToken: cancellationToken);

                await channel.Writer.WriteAsync(raw, cancellationToken);
                receivedCount += 1;
            }

            channel.Writer.Complete();
        }
        catch (Exception e)
        {
            channel.Writer.TryComplete(e);
        }
        finally
        {
            if (req.ExpectedResponses == 0)
            {
                // FIXME: Temporary fix to wait for a message to be sent.
                await Task.Delay(1000);
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

    private CommunicationFailException WrapCommunicationFailException(
        Exception innerException,
        Peer peer,
        IMessage message)
    {
        return new CommunicationFailException(
            $"Failed to send and receive replies from {peer} for request {message}.",
            message.GetType(),
            peer,
            innerException);
    }

    private class MessageRequest
    {
        public MessageRequest(
            in Guid id,
            MessageEnvelope message,
            Peer peer,
            in int expectedResponses,
            Channel<NetMQMessage> channel,
            CancellationToken cancellationToken)
        {
            Id = id;
            Message = message;
            Peer = peer;
            ExpectedResponses = expectedResponses;
            Channel = channel;
            CancellationToken = cancellationToken;
        }

        public Guid Id { get; }

        public MessageEnvelope Message { get; }

        public Peer Peer { get; }

        public int ExpectedResponses { get; }

        public Channel<NetMQMessage> Channel { get; }

        public CancellationToken CancellationToken { get; }
    }
}
