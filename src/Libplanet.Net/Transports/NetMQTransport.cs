using System.Diagnostics;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;

namespace Libplanet.Net.Transports;

public sealed class NetMQTransport(ISigner signer, TransportOptions options)
    : ITransport
{
    private readonly Channel<MessageRequest> _requestChannel = Channel.CreateUnbounded<MessageRequest>();

    private readonly Subject<IReplyContext> _processSubject = new();
    private readonly NetMQRouterSocket _router = new(signer.Address, options.Host, options.Port);
    private readonly NetMQQueue<MessageResponse> _replyQueue = new();
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);

    private NetMQPoller? _poller;
    private DealerSocketHost? _dealerSocketHost;
    private CancellationTokenSource? _cancellationTokenSource = new();
    private CancellationToken _cancellationToken;
    private Task _processTask = Task.CompletedTask;
    private bool _disposed;

    public NetMQTransport(ISigner signer)
        : this(signer, new TransportOptions())
    {
    }

    public IObservable<IReplyContext> Process => _processSubject;

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
                var task = RunRequestChannelAsync(_cancellationToken);
                runtime.Run(task);
            },
            _cancellationToken,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await _poller.StartAsync(cancellationToken);
        _dealerSocketHost = new DealerSocketHost(signer);

        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        if (_dealerSocketHost is not null)
        {
            await _dealerSocketHost.DisposeAsync();
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
            if (_dealerSocketHost is not null)
            {
                await _dealerSocketHost.DisposeAsync();
            }

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
            Trace.WriteLine("sendasync: 2");
            hasNext = response.Message.HasNext;
            yield return response.Message;
        }

        Trace.WriteLine("sendasync: 3");
        channel.Writer.TryComplete();
        Trace.WriteLine("sendasync: 4");
    }

    public void Broadcast(ImmutableArray<Peer> peers, IMessage message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        Parallel.ForEach(peers, peer => Post(peer, message));
    }

    public void Reply(MessageEnvelope requestEnvelope, IMessage message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning || _replyQueue is null)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        var messageReply = new MessageResponse
        {
            MessageEnvelope = new MessageEnvelope
            {
                Identity = requestEnvelope.Identity,
                Message = message,
                Protocol = _options.Protocol,
                Sender = Peer,
                Timestamp = DateTimeOffset.UtcNow,
            },
            Receiver = requestEnvelope.Sender,
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
            var replyContext = new NetMQReplyContext(this, messageEnvelope);
            _processSubject.OnNext(replyContext);
        }
        catch
        {
            // log
        }
    }

    private void ReplyQueue_ReceiveReady(object? sender, NetMQQueueEventArgs<MessageResponse> e)
    {
        if (e.Queue.TryDequeue(out var messageReply, TimeSpan.Zero))
        {
            var messageEnvelope = messageReply.MessageEnvelope;
            var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
            var identity = Encoding.UTF8.GetBytes(messageReply.Receiver.ToString());
            Trace.WriteLine($"reply 1: {messageReply.Receiver}");
            Trace.WriteLine($"reply 2: {ByteUtility.Hex(identity)}");
            rawMessage.Push(identity);
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

    private async Task RunRequestChannelAsync(CancellationToken cancellationToken)
    {
        var requestReader = _requestChannel.Reader;
        await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
        {
            _ = RequestMessageAsync(request, request.CancellationToken);
        }
    }

    private async Task RequestMessageAsync(MessageRequest request, CancellationToken cancellationToken)
    {
        if (_dealerSocketHost is null)
        {
            throw new InvalidOperationException("DealerSocketHost is not initialized.");
        }

        var requestWriter = request.Channel?.Writer;

        try
        {
            var dealerSocketHost = _dealerSocketHost;
            var receiver = request.Receiver;
            var requestMessageEnvelope = request.MessageEnvelope;
            var hasNext = true;

            dealerSocketHost.Send(receiver, requestMessageEnvelope);
            while (hasNext && requestWriter is not null)
            {
                var responseMessageEnvelope = await dealerSocketHost.Process.WaitAsync(
                    m => m.Identity == requestMessageEnvelope.Identity, cancellationToken);
                var messageResponse = new MessageResponse
                {
                    MessageEnvelope = responseMessageEnvelope,
                    Receiver = Peer,
                };
                Trace.WriteLine("Received 123: ");

                await requestWriter.WriteAsync(messageResponse, cancellationToken);
                hasNext = responseMessageEnvelope.Message.HasNext;
                Trace.WriteLine("Received 1234: ");
            }

            requestWriter?.Complete();
            Trace.WriteLine("Received 12345: ");
        }
        catch (Exception e)
        {
            requestWriter?.TryComplete(e);
        }

        Trace.WriteLine("Received 123456: ");
    }

    private async Task<Channel<MessageResponse>> WriteAsync(
        Peer receiver, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.SendTimeout);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken, timeoutCancellationTokenSource.Token);
        var channel = Channel.CreateUnbounded<MessageResponse>();

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
        Channel<MessageResponse> channel, CancellationToken cancellationToken)
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

    private void Post(Peer receiver, IMessage message)
    {
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken);

        var messageEnvelope = new MessageEnvelope
        {
            Identity = Guid.NewGuid(),
            Message = message,
            Protocol = _options.Protocol,
            Sender = Peer,
            Timestamp = DateTimeOffset.UtcNow,
        };
        var messageRequest = new MessageRequest
        {
            MessageEnvelope = messageEnvelope,
            Receiver = receiver,
            CancellationToken = cancellationTokenSource.Token,
        };

        _ = _requestChannel.Writer.WriteAsync(messageRequest, cancellationTokenSource.Token).AsTask();
    }
}
