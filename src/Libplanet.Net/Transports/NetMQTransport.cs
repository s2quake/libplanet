using System.Net;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace Libplanet.Net.Transports;

public sealed class NetMQTransport(ISigner signer, TransportOptions options)
    : ITransport
{
    private readonly Channel<MessageRequest> _requestChannel = Channel.CreateUnbounded<MessageRequest>();

    private readonly Subject<MessageEnvelope> _processMessageSubject = new();
    private readonly RouterSocket _router = new();
    private readonly NetMQQueue<MessageReply> _replyQueue = new();
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);
    private int _port;
    private NetMQPoller? _poller;
    private Peer? _peer;

    private CancellationTokenSource? _cancellationTokenSource = new();
    private CancellationToken _cancellationToken;
    private Task _processTask = Task.CompletedTask;

    private long _requestCount;
    private long _socketCount;
    private bool _disposed;

    public NetMQTransport(ISigner signer)
        : this(signer, new TransportOptions())
    {
    }

    public IObservable<MessageEnvelope> ProcessMessage => _processMessageSubject;

    public Peer Peer
    {
        get
        {
            if (_options.Port is 0 && !IsRunning)
            {
                throw new InvalidOperationException("Port is not set and transport is not running.");
            }

            return _peer ??= new()
            {
                Address = signer.Address,
                EndPoint = new DnsEndPoint(_options.Host, _port),
            };
        }
    }

    public bool IsRunning { get; private set; }

    public Protocol Protocol => _options.Protocol;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            throw new InvalidOperationException("Transport is already running.");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;
        _port = Initialize(_router, _options.Port);
        _poller = [_router, _replyQueue];
        _router.ReceiveReady += Router_ReceiveReady;
        _replyQueue.ReceiveReady += ReplyQueue_ReceiveReady;
        _processTask = Task.Run(() =>
        {
            using var runtime = new NetMQRuntime();
            var task = ProcessRequestAsync(_cancellationToken);
            runtime.Run(task);
        }, _cancellationToken);
        await _poller.StartAsync(cancellationToken);

        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        if (_poller is not null)
        {
            await _poller.StopAsync(cancellationToken);
            _poller = null;
        }

        await TaskUtility.TryWait(_processTask);
        _replyQueue.ReceiveReady -= ReplyQueue_ReceiveReady;
        _router.ReceiveReady -= Router_ReceiveReady;
        _processTask = Task.CompletedTask;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _peer = null;
        _requestCount = 0;
        IsRunning = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _requestChannel.Writer.TryComplete();

            _replyQueue.ReceiveReady -= ReplyQueue_ReceiveReady;
            _router.ReceiveReady -= Router_ReceiveReady;
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            if (_poller is not null)
            {
                await _poller.DisposeAsync();
                _poller = null;
            }

            _router.Unbind($"tcp://*:{_port}");
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _replyQueue.Dispose();
            _router.Dispose();
            _processMessageSubject.Dispose();

            _disposed = true;
        }
    }

    public async Task<MessageEnvelope> SendMessageAsync(
        Peer peer, IMessage message, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.SendTimeout);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken, timeoutCancellationTokenSource.Token);
        var channel = Channel.CreateUnbounded<MessageReply>();

        try
        {
            var request = new MessageRequest
            {
                MessageEnvelope = new MessageEnvelope
                {
                    Identity = Guid.NewGuid(),
                    Message = message,
                    Protocol = _options.Protocol,
                    Peer = Peer,
                    Timestamp = DateTimeOffset.UtcNow,
                },
                Peer = peer,
                Channel = channel,
                CancellationToken = cancellationTokenSource.Token,
            };
            Interlocked.Increment(ref _requestCount);
            // cancellationTokenSource.CancelAfter(_options.SendTimeout);
            await _requestChannel.Writer.WriteAsync(request, cancellationTokenSource.Token);

            var reply = await channel.Reader.ReadAsync(cancellationTokenSource.Token);
            var messageEnvelope = reply.MessageEnvelope;
            messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
            return messageEnvelope;
        }
        catch (OperationCanceledException e) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new TimeoutException("The request timed out while waiting for a response.", e);
        }
        catch (ChannelClosedException e)
        {
            throw new CommunicationException("The channel was closed before a response could be received.", e);
        }
        finally
        {
            channel.Writer.TryComplete();
        }
    }

    public async IAsyncEnumerable<IMessage> SendMessageAsStreamAsync(
        Peer peer, IMessage message, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.SendTimeout);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken, timeoutCancellationTokenSource.Token);
        var channel = Channel.CreateUnbounded<MessageReply>();

        try
        {
            var request = new MessageRequest
            {
                MessageEnvelope = new MessageEnvelope
                {
                    Identity = Guid.NewGuid(),
                    Message = message,
                    Protocol = _options.Protocol,
                    Peer = Peer,
                    Timestamp = DateTimeOffset.UtcNow,
                },
                Peer = peer,
                Channel = channel,
                CancellationToken = cancellationTokenSource.Token,
            };
            Interlocked.Increment(ref _requestCount);
            await _requestChannel.Writer.WriteAsync(request, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException e) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            channel.Writer.TryComplete();
            throw new TimeoutException("The request timed out while waiting for a response.", e);
        }
        catch (ChannelClosedException e)
        {
            channel.Writer.TryComplete();
            throw new CommunicationException("The channel was closed before a response could be received.", e);
        }

        try
        {
            var hasNext = true;
            while (hasNext)
            {
                var reply = await channel.Reader.ReadAsync(cancellationToken);
                var messageEnvelope = reply.MessageEnvelope;
                messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
                hasNext = messageEnvelope.Message.HasNext;
                yield return messageEnvelope.Message;
            }
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

    public void ReplyMessage(Guid identity, IMessage message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning || _replyQueue is null)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        var messageReply = new MessageReply
        {
            MessageEnvelope = new MessageEnvelope
            {
                Identity = identity,
                Message = message,
                Protocol = _options.Protocol,
                Peer = Peer,
                Timestamp = DateTimeOffset.UtcNow,
            },
        };
        _replyQueue.Enqueue(messageReply);
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

    private void Router_ReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        try
        {
            var receivedMessage = new NetMQMessage();
            if (!e.Socket.TryReceiveMultipartMessage(TimeSpan.Zero, ref receivedMessage))
            {
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var rawMessage = new NetMQMessage(receivedMessage.Skip(1));
            var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
            messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
            _processMessageSubject.OnNext(messageEnvelope);
        }
        catch
        {
            // log
        }
    }

    private void ReplyQueue_ReceiveReady(object? sender, NetMQQueueEventArgs<MessageReply> e)
    {
        if (e.Queue.TryDequeue(out var messageReply, TimeSpan.Zero))
        {
            var messageEnvelope = messageReply.MessageEnvelope;
            var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
            rawMessage.Push(messageEnvelope.Identity.ToByteArray());
            if (_router.TrySendMultipartMessage(TimeSpan.FromSeconds(1), rawMessage))
            {
                // Successfully sent the message
            }
        }
        else
        {
            // Handle the case where no message was dequeued
        }
    }

    private async Task ProcessRequestAsync(CancellationToken cancellationToken)
    {
        var requestReader = _requestChannel.Reader;
        await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _requestCount);
            _ = RequestMessageAsync(request, request.CancellationToken);
        }
    }

    private async Task RequestMessageAsync(MessageRequest request, CancellationToken cancellationToken)
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

            var rawMessage = NetMQMessageCodec.Encode(request.MessageEnvelope, signer);
            if (!dealerSocket.TrySendMultipartMessage(rawMessage))
            {
                throw new InvalidOperationException();
            }

            var hasNext = true;
            while (hasNext)
            {
                var receivedRawMessage = await dealerSocket.ReceiveMultipartMessageAsync(
                    expectedFrameCount: 3,
                    cancellationToken: cancellationToken);
                var messageEnvelope = NetMQMessageCodec.Decode(receivedRawMessage);
                var reply = new MessageReply
                {
                    MessageEnvelope = messageEnvelope,
                };

                await requestWriter.WriteAsync(reply, cancellationToken);
                hasNext = messageEnvelope.Message.HasNext;
            }

            requestWriter.Complete();
        }
        catch (Exception e)
        {
            requestWriter.TryComplete(e);
        }
        finally
        {
            if (incrementedSocketCount is { })
            {
                Interlocked.Decrement(ref _socketCount);
            }
        }
    }

    private sealed record class MessageRequest
    {
        public required MessageEnvelope MessageEnvelope { get; init; }

        public required Peer Peer { get; init; }

        public required Channel<MessageReply> Channel { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed record class MessageReply
    {
        public required MessageEnvelope MessageEnvelope { get; init; }
    }
}
