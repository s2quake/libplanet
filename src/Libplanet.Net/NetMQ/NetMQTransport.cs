using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using NetMQ;

namespace Libplanet.Net.NetMQ;

public sealed class NetMQTransport(ISigner signer, TransportOptions options)
    : ServiceBase, ITransport
{
    private readonly NetMQReceiver _receiver = new(signer.Address, options.Host, options.Port);
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);
    private readonly ProtocolHash _protocolHash = options.Protocol.Hash;
    private NetMQRequestWorker? _requestWorker;
    // private NetMQPoller? _poller;
    private IDisposable? _subscription;

    public NetMQTransport(ISigner signer)
        : this(signer, new TransportOptions())
    {
    }

    public MessageRouter MessageHandlers { get; } = [];

    public Peer Peer => _receiver.Peer;

    public Protocol Protocol => _options.Protocol;

    CancellationToken ITransport.StoppingToken => StoppingToken;

    IMessageRouter ITransport.MessageRouter => MessageHandlers;

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
            ProtocolHash = _protocolHash,
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

    // private void Router_ReceiveReady(object? sender, NetMQSocketEventArgs e)
    // {
    //     try
    //     {
    //         Trace.WriteLine("Router_ReceiveReady");
    //         var receivedMessage = new NetMQMessage();
    //         if (!e.Socket.TryReceiveMultipartMessage(TimeSpan.Zero, ref receivedMessage))
    //         {
    //             return;
    //         }

    //         var rawMessage = new NetMQMessage(receivedMessage.Skip(1));
    //         var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
    //         messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
    //         MessageHandlers.Handle(messageEnvelope);
    //         Trace.WriteLine($"Received message: {messageEnvelope.Identity}");
    //     }
    //     catch
    //     {
    //         // log
    //     }
    // }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _requestWorker = new NetMQRequestWorker(signer);
        _subscription = _receiver.Received.Subscribe(MessageHandlers.Handle);
        await _receiver.StartAsync(default);
        // _poller = [_receiver];
        // _receiver.ReceiveReady += Router_ReceiveReady;
        // await _poller.StartAsync(cancellationToken);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        await _receiver.StopAsync(cancellationToken);

        // _receiver.ReceiveReady -= Router_ReceiveReady;
        // if (_poller is not null)
        // {
        //     _poller.Remove(_receiver);
        //     await _poller.StopAsync(cancellationToken);
        //     _poller = null;
        // }

        if (_requestWorker is not null)
        {
            await _requestWorker.DisposeAsync();
            _requestWorker = null;
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        // _responseQueue.ReceiveReady -= ResponseQueue_ReceiveReady;
        // _receiver.ReceiveReady -= Router_ReceiveReady;

        // if (_poller is not null)
        // {
        //     await _poller.DisposeAsync();
        //     _poller = null;
        // }

        if (_requestWorker is not null)
        {
            await _requestWorker.DisposeAsync();
            _requestWorker = null;
        }

        // _responseQueue.Dispose();
        await _receiver.DisposeAsync();
        await base.DisposeAsyncCore();
    }
}
