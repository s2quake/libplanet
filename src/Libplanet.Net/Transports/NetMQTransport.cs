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

namespace Libplanet.Net.Transports;

public sealed class NetMQTransport(PrivateKey privateKey, ProtocolOptions protocolOptions, HostOptions hostOptions)
    : ITransport
{
    private readonly NetMQMessageCodec _messageCodec = new();
    private readonly Channel<MessageRequest> _requestChannel = Channel.CreateUnbounded<MessageRequest>();

    private RouterSocket? _router;
    private int _port;
    private NetMQPoller? _routerPoller;
    private NetMQRuntime? _runtime;
    private NetMQQueue<MessageReply>? _replyQueue;
    private Peer? _peer;

    private CancellationTokenSource? _cancellationTokenSource = new();
    private CancellationToken _cancellationToken;

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

        _router = new RouterSocket();
        _router.Options.RouterHandover = true;
        _port = Initialize(_router, hostOptions.Port);

        _runtime = new NetMQRuntime();
        _runtime.Run(ProcessRuntimeAsync(_cancellationTokenSource.Token));
        _replyQueue = new();
        _routerPoller = [_router, _replyQueue];

        _router.ReceiveReady += Router_ReceiveReady;
        _replyQueue.ReceiveReady += ReplyQueue_ReceiveReady;

        _routerPoller.Run();
        while (!_routerPoller.IsRunning)
        {
            await Task.Yield();
        }

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

        if (_routerPoller is not null)
        {
            _routerPoller.Stop();
            while (_routerPoller.IsRunning)
            {
                await Task.Yield();
            }
            _routerPoller.Dispose();
            _routerPoller = null;
        }

        if (_replyQueue is not null)
        {
            _replyQueue.ReceiveReady -= ReplyQueue_ReceiveReady!;
            _replyQueue.Dispose();
            _replyQueue = null;
        }

        if (_runtime is not null)
        {
            _runtime.Dispose();
            _runtime = null;
        }

        if (_router is not null)
        {
            _router.ReceiveReady -= Router_ReceiveReady!;
            _router.Dispose();
            _router = null;
        }

        IsRunning = false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _requestChannel.Writer.TryComplete();

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            if (_routerPoller is not null)
            {
                _routerPoller.Stop();
                _routerPoller.Dispose();
            }

            if (_replyQueue is not null)
            {
                _replyQueue.ReceiveReady -= ReplyQueue_ReceiveReady!;
                _replyQueue.Dispose();
                _replyQueue = null;
            }

            if (_runtime is not null)
            {
                _runtime.Dispose();
                _runtime = null;
            }

            if (_router is not null)
            {
                _router.ReceiveReady -= Router_ReceiveReady!;
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

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken);
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
                    Protocol = protocolOptions.Protocol,
                    Remote = Peer,
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
                var messageEnvelope = _messageCodec.Decode(rawMessage);

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

    public async void BroadcastMessage(IEnumerable<Peer> peers, IMessage message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationToken);
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));
            await peers.ParallelForEachAsync(
                peer => SendMessageAsync(peer, message, cancellationTokenSource.Token),
                cancellationTokenSource.Token);
        }
        catch
        {
            // do nothing
        }
    }

    public async Task ReplyMessageAsync(IMessage message, Guid id, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_cancellationTokenSource is null)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationTokenSource.Token, cancellationToken);

        var messageReply = new MessageReply
        {
            ResetEvent = new AsyncManualResetEvent(),
            MessageEnvelope = new MessageEnvelope
            {
                Id = id,
                Message = message,
                Protocol = protocolOptions.Protocol,
                Remote = Peer,
                Timestamp = DateTimeOffset.UtcNow,
            },
        };
        _replyQueue!.Enqueue(messageReply);

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

                var rawMessage2 = new NetMQMessage(rawMessage.Select(f => f.Duplicate()));
                var messageEnvelope = _messageCodec.Decode(rawMessage2);
                messageEnvelope.Validate(protocolOptions.Protocol, protocolOptions.MessageLifetime);
                await ProcessMessageHandler.InvokeAsync(messageEnvelope);
            }
        }
        catch
        {
            // log
        }
    }

    private void ReplyQueue_ReceiveReady(object? sender, NetMQQueueEventArgs<MessageReply> e)
    {
        var messageReply = e.Queue.Dequeue();
        var messageEnvelope = messageReply.MessageEnvelope;
        var rawMessage = _messageCodec.Encode(messageEnvelope, privateKey);
        if (_router is not null && _router.TrySendMultipartMessage(TimeSpan.FromSeconds(1), rawMessage))
        {
            messageReply.Set();
        }
    }

    private async Task ProcessRuntimeAsync(CancellationToken cancellationToken)
    {
        var reader = _requestChannel.Reader;
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
            using var dealerSocket = new DealerSocket();
            dealerSocket.Options.DisableTimeWait = true;
            dealerSocket.Options.Identity = request.SocketId.ToByteArray();
            dealerSocket.Connect(address);
            incrementedSocketCount = Interlocked.Increment(ref _socketCount);

            var rawMessage = _messageCodec.Encode(request.MessageEnvelope, privateKey);
            if (!dealerSocket.TrySendMultipartMessage(rawMessage))
            {
                throw new InvalidOperationException();
            }

            for (var i = 0; i < request.ExpectedResponses; i++)
            {
                NetMQMessage raw = await dealerSocket.ReceiveMultipartMessageAsync(
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

    private sealed record class MessageRequest
    {
        public required Guid SocketId { get; init; }

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
