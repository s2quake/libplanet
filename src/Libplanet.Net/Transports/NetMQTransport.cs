using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncIO;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;
using NetMQ.Sockets;
using Nito.AsyncEx;

namespace Libplanet.Net.Transports;

public sealed class NetMQTransport(PrivateKey privateKey, ProtocolOptions protocolOptions, HostOptions hostOptions)
    : ITransport
{
    private readonly Channel<MessageRequest> _requestChannel = Channel.CreateUnbounded<MessageRequest>();

    private RouterSocket _router = new RouterSocket();
    private int _port;
    private NetMQPoller? _poller;
    private NetMQQueue<MessageReply>? _replyQueue;
    private Peer? _peer;

    private CancellationTokenSource? _cancellationTokenSource = new();
    private CancellationToken _cancellationToken;
    private Task _processTask = Task.CompletedTask;
    private Task _pollerTask = Task.CompletedTask;

    private long _requestCount;
    private long _socketCount;
    private bool _disposed;

    static NetMQTransport()
    {
        NetMQConfig.ThreadPoolSize = 3;
        ForceDotNet.Force();
    }

    public AsyncDelegate<MessageEnvelope> ProcessMessageHandler { get; } = new AsyncDelegate<MessageEnvelope>();

    public Peer Peer
    {
        get
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("Transport is not running.");
            }

            return _peer ??= new()
            {
                Address = privateKey.Address,
                EndPoint = new DnsEndPoint(hostOptions.Host, _port),
            };
        }
    }

    public bool IsRunning { get; private set; }

    public Protocol Protocol => protocolOptions.Protocol;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            throw new InvalidOperationException("Transport is already running.");
        }

        _cancellationTokenSource = new CancellationTokenSource();

        // _router = new RouterSocket();
        // _router.Options.RouterHandover = true;
        _port = Initialize(_router, hostOptions.Port);
        _ = RunProcessAsync(_cancellationTokenSource.Token);
        _replyQueue = new();
        _poller = [_router, _replyQueue];

        _router.ReceiveReady += Router_ReceiveReady;
        _replyQueue.ReceiveReady += ReplyQueue_ReceiveReady;

        _pollerTask = Task.Run(_poller.Run, _cancellationTokenSource.Token);
        while (!_poller.IsRunning)
        {
            await Task.Yield();
        }

        _router.ReceiveReady += Router_ReceiveReady;
        _replyQueue.ReceiveReady += ReplyQueue_ReceiveReady;
        _cancellationToken = _cancellationTokenSource.Token;
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        _peer = null;
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        if (_poller is not null)
        {
            _poller.Stop();
            while (_poller.IsRunning)
            {
                await Task.Yield();
            }
            _poller.Dispose();
            _poller = null;
        }

        if (_replyQueue is not null)
        {
            _replyQueue.ReceiveReady -= ReplyQueue_ReceiveReady!;
            _replyQueue.Dispose();
            _replyQueue = null;
        }

        if (_router is not null)
        {
            _router.ReceiveReady -= Router_ReceiveReady!;
            _router.Dispose();
            _router = null;
        }

        _requestCount = 0;
        IsRunning = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _requestChannel.Writer.TryComplete();

            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            _cancellationTokenSource = null;

            _poller?.StopAsync();
            
            _poller?.Dispose();
            _poller = null;

            _cancellationTokenSource?.Dispose();

            if (_replyQueue is not null)
            {
                _replyQueue.ReceiveReady -= ReplyQueue_ReceiveReady!;
                _replyQueue.Dispose();
                _replyQueue = null;
            }

            if (_router is not null)
            {
                _router.ReceiveReady -= Router_ReceiveReady!;
                _router.Unbind($"tcp://*:{_port}");
                _router.Dispose();
                _router = null;
            }

            _disposed = true;
        }
    }

    public async Task<MessageEnvelope> SendMessageAsync(
        Peer peer, IMessage message, CancellationToken cancellationToken)
    {
        IEnumerable<MessageEnvelope> replies =
            await SendMessageAsync(
                peer,
                message,
                1,
                cancellationToken).ConfigureAwait(false);
        MessageEnvelope reply = replies.First();

        return reply;
    }

    public async Task<IEnumerable<MessageEnvelope>> SendMessageAsync(
        Peer peer, IMessage message, int expectedResponses, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken);
        var replyList = new List<MessageEnvelope>();
        var channel = Channel.CreateUnbounded<NetMQMessage>();

        try
        {
            var request = new MessageRequest
            {
                MessageEnvelope = new MessageEnvelope
                {
                    Identity = Guid.NewGuid(),
                    Message = message,
                    Protocol = protocolOptions.Protocol,
                    Peer = Peer,
                    Timestamp = DateTimeOffset.UtcNow,
                },
                Peer = peer,
                ExpectedResponses = expectedResponses,
                Channel = channel,
                CancellationToken = cancellationTokenSource.Token,
            };
            Interlocked.Increment(ref _requestCount);
            await _requestChannel.Writer.WriteAsync(request, cancellationTokenSource.Token);

            for (var i = 0; i < expectedResponses; i++)
            {
                var rawMessage = await channel.Reader.ReadAsync(cancellationTokenSource.Token);
                var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);

                try
                {
                    messageEnvelope.Validate(protocolOptions.Protocol, protocolOptions.MessageLifetime);
                }
                catch (InvalidOperationException dapve)
                {
                    channel.Writer.Complete(dapve);
                }

                replyList.Add(messageEnvelope);
            }

            return replyList;
        }
        finally
        {
            channel.Writer.TryComplete();
        }
    }

    public void BroadcastMessage(IEnumerable<Peer> peers, IMessage message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        Invoke();

        async void Invoke()
        {
            try
            {
                using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    _cancellationToken);
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));
                await Parallel.ForEachAsync(peers,
                    cancellationTokenSource.Token,
                    async (peer, cancellationToken) => await SendMessageAsync(peer, message, cancellationToken));
            }
            catch
            {
                // do nothing
            }
        }
    }

    public async Task ReplyMessageAsync(IMessage message, Guid id, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning || _replyQueue is null)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken);

        var messageReply = new MessageReply
        {
            ResetEvent = new AsyncManualResetEvent(),
            MessageEnvelope = new MessageEnvelope
            {
                Identity = id,
                Message = message,
                Protocol = protocolOptions.Protocol,
                Peer = Peer,
                Timestamp = DateTimeOffset.UtcNow,
            },
        };
        _replyQueue.Enqueue(messageReply);

        await messageReply.WaitAsync(cancellationTokenSource.Token);
    }

    private static int Initialize(RouterSocket routerSocket, int port)
    {
        if (port == 0)
        {
            return routerSocket.BindRandomPort("tcp://*");
        }

        routerSocket.Bind($"tcp://*:{port}");
        return port;
    }

    private async void Router_ReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        try
        {
            var rawMessage = new NetMQMessage();
            for (var i = 0; i < 1_000; i++)
            {
                if (!e.Socket.TryReceiveMultipartMessage(TimeSpan.Zero, ref rawMessage))
                {
                    break;
                }

                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var rawMessage2 = new NetMQMessage(rawMessage.Skip(1));
                var messageEnvelope = NetMQMessageCodec.Decode(rawMessage2);
                messageEnvelope.Validate(protocolOptions.Protocol, protocolOptions.MessageLifetime);
                await ProcessMessageHandler.InvokeAsync(messageEnvelope);
            }
        }
        catch
        {
            // log
        }
    }

    private async void ReplyQueue_ReceiveReady(object? sender, NetMQQueueEventArgs<MessageReply> e)
    {
        var messageReply = e.Queue.Dequeue();
        var messageEnvelope = messageReply.MessageEnvelope;
        var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, privateKey);
        rawMessage.Push(messageEnvelope.Identity.ToByteArray());
        if (_router?.TrySendMultipartMessage(TimeSpan.FromSeconds(1), rawMessage) is true)
        {

        }

        messageReply.Set();
    }

    private async Task ProcessRuntimeAsync(CancellationToken cancellationToken)
    {
        var requestReader = _requestChannel.Reader;
        var synchronizationContext = SynchronizationContext.Current;
        await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _requestCount);
            await synchronizationContext.PostAsync(
                () => ProcessRequestAsync(request, request.CancellationToken));
        }
    }

    private async Task ProcessRequestAsync(MessageRequest request, CancellationToken cancellationToken)
    {
        var requestChannel = request.Channel;
        var requestWriter = requestChannel.Writer;
        long? incrementedSocketCount = null;

        try
        {
            var peer = request.Peer;
            var address = await peer.ResolveNetMQAddressAsync(cancellationToken);
            using var dealerSocket = new DealerSocket();
            dealerSocket.Options.DisableTimeWait = true;
            dealerSocket.Options.Identity = request.MessageEnvelope.Identity.ToByteArray();
            dealerSocket.Connect(address);
            incrementedSocketCount = Interlocked.Increment(ref _socketCount);

            var rawMessage = NetMQMessageCodec.Encode(request.MessageEnvelope, privateKey);
            // rawMessage.Push(request.MessageEnvelope.Identity.ToByteArray());
            if (!dealerSocket.TrySendMultipartMessage(rawMessage))
            {
                throw new InvalidOperationException();
            }

            for (var i = 0; i < request.ExpectedResponses; i++)
            {
                var receivedRawMessage = await dealerSocket.ReceiveMultipartMessageAsync(
                    expectedFrameCount: 3,
                    cancellationToken: cancellationToken);

                await requestWriter.WriteAsync(receivedRawMessage, cancellationToken);
            }

            requestWriter.Complete();
        }
        catch (Exception e)
        {
            requestWriter.TryComplete(e);
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

    private async Task RunProcessAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            using var runtime = new NetMQRuntime();
            var task = ProcessRuntimeAsync(cancellationToken);
            runtime.Run(task);
        }, cancellationToken);
    }

    private sealed record class MessageRequest
    {
        public required MessageEnvelope MessageEnvelope { get; init; }

        public required Peer Peer { get; init; }

        public required int ExpectedResponses { get; init; }

        public required Channel<NetMQMessage> Channel { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed record class MessageReply
    {
        public required AsyncManualResetEvent ResetEvent { get; init; }

        public required MessageEnvelope MessageEnvelope { get; init; }

        public void Set() => ResetEvent.Set();

        public Task WaitAsync(CancellationToken cancellationToken) => ResetEvent.WaitAsync(cancellationToken);
    }
}
