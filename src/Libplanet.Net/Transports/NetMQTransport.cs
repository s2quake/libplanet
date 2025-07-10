using System.Diagnostics;
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

    private readonly Subject<MessageEnvelope> _processSubject = new();
    private readonly NetMQRouterSocket _router = new(signer.Address, options.Host, options.Port);
    private readonly NetMQQueue<MessageReply> _replyQueue = new();
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);
    private NetMQPoller? _poller;

    private CancellationTokenSource? _cancellationTokenSource = new();
    private CancellationToken _cancellationToken;
    private Task _processTask = Task.CompletedTask;

    private long _socketCount;
    private bool _disposed;

    public NetMQTransport(ISigner signer)
        : this(signer, new TransportOptions())
    {
    }

    public IObservable<MessageEnvelope> Process => _processSubject;

    public Peer Peer => _router.Peer;

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
        _poller = [_router, _replyQueue];
        _router.ReceiveReady += Router_ReceiveReady;
        _replyQueue.ReceiveReady += ReplyQueue_ReceiveReady;
        _processTask = Task.Factory.StartNew(
            () =>
            {
                using var runtime = new NetMQRuntime();
                var task = ProcessRequestAsync(_cancellationToken);
                runtime.Run(task);
            },
            _cancellationToken,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
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

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _replyQueue.Dispose();
            _router.Dispose();
            _processSubject.Dispose();

            _disposed = true;
        }
    }

    private async Task<Channel<MessageReply>> WriteAsync(
        Peer receiver, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.SendTimeout);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken, timeoutCancellationTokenSource.Token);
        var channel = Channel.CreateUnbounded<MessageReply>();

        try
        {
            var messageRequest = new MessageRequest
            {
                MessageEnvelope = messageEnvelope,
                Receiver = receiver,
                Channel = channel,
                CancellationToken = cancellationTokenSource.Token,
            };
            await _requestChannel.Writer.WriteAsync(messageRequest, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException e) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            channel.Writer.TryComplete(e);
            throw new TimeoutException("The request timed out while waiting for a response.", e);
        }
        catch (ChannelClosedException e)
        {
            channel.Writer.TryComplete(e);
            throw new CommunicationException("The channel was closed before a response could be received.", e);
        }
        catch (Exception e)
        {
            channel.Writer.TryComplete(e);
            throw;
        }

        return channel;
    }

    private async Task<MessageEnvelope> ReadAsync(
        Channel<MessageReply> channel, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.ReceiveTimeout);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken, timeoutCancellationTokenSource.Token);

        try
        {
            var reply = await channel.Reader.ReadAsync(cancellationTokenSource.Token);
            var messageEnvelope = reply.MessageEnvelope;
            messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
            return messageEnvelope;
        }
        catch (OperationCanceledException e) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            channel.Writer.TryComplete(e);
            throw new TimeoutException("The read operation timed out while waiting for a response.", e);
        }
        catch (ChannelClosedException e)
        {
            channel.Writer.TryComplete(e);
            throw new CommunicationException("The channel was closed before a response could be received.", e);
        }
        catch (Exception e)
        {
            channel.Writer.TryComplete(e);
            throw new InvalidOperationException("An error occurred while reading the message.", e);
        }
    }

    public async IAsyncEnumerable<IMessage> SendAsync(
        Peer receiver, IMessage message, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        var messageEnvelope = new MessageEnvelope
        {
            Identity = Guid.NewGuid(),
            Message = message,
            Protocol = _options.Protocol,
            Sender = Peer,
            Timestamp = DateTimeOffset.UtcNow,
        };
        var channel = await WriteAsync(receiver, messageEnvelope, cancellationToken);
        var hasNext = true;
        while (hasNext)
        {
            var response = await ReadAsync(channel, cancellationToken);
            hasNext = response.Message.HasNext;
            yield return response.Message;
        }

        channel.Writer.TryComplete();
    }

    public void Broadcast(ImmutableArray<Peer> peers, IMessage message)
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

                await Parallel.ForEachAsync(
                    peers,
                    cancellationTokenSource.Token,
                    async (peer, cancellationToken) =>
                    {
                        Trace.WriteLine("Broadcasting message to: " + peer);
                        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken);
                        _ = SendAsync(peer, message, cancellationTokenSource.Token);
                        // cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));
                        try
                        {

                            {
                                // do nothing
                            }
                        }
                        catch
                        {

                        }
                    });
            }
            catch
            {
                // do nothing
            }
        }
    }

    public void Reply(Guid identity, IMessage message)
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
                Sender = Peer,
                Timestamp = DateTimeOffset.UtcNow,
            },
        };
        _replyQueue.Enqueue(messageReply);
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
            Trace.WriteLine($"Received {messageEnvelope.Message.GetType().Name}: {messageEnvelope.Sender}");
            _processSubject.OnNext(messageEnvelope);
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
            var peer = request.Receiver;
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

        public required Peer Receiver { get; init; }

        public required Channel<MessageReply> Channel { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed record class MessageReply
    {
        public required MessageEnvelope MessageEnvelope { get; init; }
    }
}
