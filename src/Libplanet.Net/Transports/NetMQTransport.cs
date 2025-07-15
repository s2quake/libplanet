using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using NetMQ;

namespace Libplanet.Net.Transports;

public sealed class NetMQTransport(ISigner signer, TransportOptions options)
    : ITransport
{
    private readonly Subject<IReplyContext> _processSubject = new();
    private readonly NetMQRouterSocket _router = new(signer.Address, options.Host, options.Port);
    private readonly NetMQQueue<MessageResponse> _responseQueue = new();
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);

    private CancellationTokenSource? _cancellationTokenSource = new();
    private CancellationToken _cancellationToken;
    private NetMQRequestWorker? _requestWorker;
    private NetMQPoller? _poller;
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
        _requestWorker = new NetMQRequestWorker(signer);
        _poller = [_router, _responseQueue];
        _router.ReceiveReady += Router_ReceiveReady;
        _responseQueue.ReceiveReady += ResponseQueue_ReceiveReady;
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

        _responseQueue.ReceiveReady -= ResponseQueue_ReceiveReady;
        _router.ReceiveReady -= Router_ReceiveReady;
        if (_poller is not null)
        {
            await _poller.StopAsync(cancellationToken);
            _poller.Dispose();
            _poller = null;
        }

        if (_requestWorker is not null)
        {
            await _requestWorker.DisposeAsync();
            _requestWorker = null;
        }

        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        IsRunning = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _responseQueue.ReceiveReady -= ResponseQueue_ReceiveReady;
            _router.ReceiveReady -= Router_ReceiveReady;

            if (_poller is not null)
            {
                await _poller.DisposeAsync();
                _poller = null;
            }

            if (_requestWorker is not null)
            {
                await _requestWorker.DisposeAsync();
                _requestWorker = null;
            }

            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _responseQueue.Dispose();
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
            hasNext = response.HasNext;
            yield return response.MessageEnvelope.Message;
        }

        channel.Writer.TryComplete();
    }

    public void Send(Peer receiver, IMessage message)
    {
        if (_requestWorker is null)
        {
            throw new InvalidOperationException("Transport is not running");
        }

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

        _ = _requestWorker.WriteAsync(messageRequest, cancellationTokenSource.Token);
    }

    public void Send(ImmutableArray<Peer> receivers, IMessage message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        Parallel.ForEach(receivers, peer => Send(peer, message));
    }

    public void Reply(MessageEnvelope requestEnvelope, IMessage message, bool hasNext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning || _responseQueue is null)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        var messageResponse = new MessageResponse
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
            HasNext = hasNext,
        };
        _responseQueue.Enqueue(messageResponse);
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
            var replyContext = new NetMQReplyContext(this, messageEnvelope);
            _processSubject.OnNext(replyContext);
        }
        catch
        {
            // log
        }
    }

    private void ResponseQueue_ReceiveReady(object? sender, NetMQQueueEventArgs<MessageResponse> e)
    {
        if (e.Queue.TryDequeue(out var messageResponse, TimeSpan.Zero))
        {
            var messageEnvelope = messageResponse.MessageEnvelope;
            var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
            var identity = Encoding.UTF8.GetBytes(messageResponse.Receiver.ToString());
            rawMessage.Push(identity);
            rawMessage.Append(messageResponse.HasNext ? 1 : 0);
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

    private async Task<Channel<MessageResponse>> WriteAsync(
        Peer receiver, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        if (_requestWorker is null)
        {
            throw new InvalidOperationException("Transport is not running");
        }

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
            await _requestWorker.WriteAsync(messageRequest, cancellationTokenSource.Token);
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

    private async Task<MessageResponse> ReadAsync(
        Channel<MessageResponse> channel, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.SendTimeout);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken, timeoutCancellationTokenSource.Token);

        try
        {
            var response = await channel.Reader.ReadAsync(cancellationTokenSource.Token);
            var messageEnvelope = response.MessageEnvelope;
            messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
            return response;
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
            throw;
        }
    }
}
