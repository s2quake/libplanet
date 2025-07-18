using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using NetMQ;

namespace Libplanet.Net.Transports;

public sealed class NetMQTransport(ISigner signer, TransportOptions options)
    : ServiceBase, ITransport
{
    private readonly NetMQRouterSocket _router = new(signer.Address, options.Host, options.Port);
    private readonly NetMQQueue<MessageResponse> _responseQueue = new();
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);
    private NetMQRequestWorker? _requestWorker;
    private NetMQPoller? _poller;

    public NetMQTransport(ISigner signer)
        : this(signer, new TransportOptions())
    {
    }

    public MessageHandlerCollection MessageHandlers { get; } = [];

    public Peer Peer => _router.Peer;

    CancellationToken ITransport.StoppingToken => StoppingToken;

    public MessageEnvelope Post(Peer receiver, IMessage message, Guid? replyTo)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!IsRunning || _requestWorker is null)
        {
            throw new InvalidOperationException("Transport is not running");
        }

        var messageEnvelope = new MessageEnvelope
        {
            Identity = Guid.NewGuid(),
            Message = message,
            Protocol = _options.Protocol,
            Sender = Peer,
            Timestamp = DateTimeOffset.UtcNow,
            ReplyTo = replyTo,
            Lifespan = _options.MessageLifetime,
            ReplyTimeout = _options.ReplyTimeout,
        };
        var messageRequest = new MessageRequest
        {
            MessageEnvelope = messageEnvelope,
            Receiver = receiver,
        };

        _ = _requestWorker.WriteAsync(messageRequest, default);
        return messageEnvelope;
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

            var rawMessage = new NetMQMessage(receivedMessage.Skip(1));
            var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
            messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
            MessageHandlers.Handle(messageEnvelope);
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

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _requestWorker = new NetMQRequestWorker(signer);
        _poller = [_router, _responseQueue];
        _router.ReceiveReady += Router_ReceiveReady;
        _responseQueue.ReceiveReady += ResponseQueue_ReceiveReady;
        await _poller.StartAsync(cancellationToken);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
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
    }

    protected override async ValueTask DisposeAsyncCore()
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

        _responseQueue.Dispose();
        _router.Dispose();
        await base.DisposeAsyncCore();
    }
}
