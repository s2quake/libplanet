using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

[Obsolete("Incomplete")]
public sealed class Transport(ISigner signer, TransportOptions options) : ServiceBase, ITransport
{
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);
    private readonly MessageRouter _messageRouter = new();
    private readonly ProtocolHash _protocolHash = options.Protocol.Hash;
    private readonly TransportPeer _peer = new(signer.Address, options.Host, options.Port);
    private Channel<MessageRequest>? _sendChannel;
    private Channel<MessageEnvelope>? _receiveChannel;
    private UdpClient? _client;
    private Task[] _processTasks = [];

    IMessageRouter ITransport.MessageRouter => _messageRouter;

    public Peer Peer => _peer;

    public Protocol Protocol => options.Protocol;

    CancellationToken ITransport.StoppingToken => StoppingToken;

    public MessageEnvelope Post(Peer receiver, IMessage message, Guid? replyTo)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_sendChannel is null)
        {
            throw new InvalidOperationException("Transport is not initialized.");
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
            Receiver = receiver,
            MessageEnvelope = messageEnvelope,
        };

        _sendChannel.Writer.TryWrite(messageRequest);
        return messageEnvelope;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        _sendChannel = Channel.CreateUnbounded<MessageRequest>();
        _receiveChannel = Channel.CreateUnbounded<MessageEnvelope>();
        _client = new UdpClient(Peer.EndPoint.Port);
        _processTasks =
        [
            ProcessReceiveAsync(_client, _receiveChannel, StoppingToken),
            ProcessSendAsync(_client, _sendChannel, StoppingToken),
            ProcessHandleAsync(_receiveChannel, _messageRouter, StoppingToken),
        ];
        return Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _sendChannel?.Writer.Complete();
        _sendChannel = null;
        _receiveChannel?.Writer.Complete();
        _receiveChannel = null;
        await TaskUtility.TryWhenAll(_processTasks);
        _processTasks = [];
        _client?.Dispose();
        _client = null;
        await Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _sendChannel?.Writer.Complete();
        _sendChannel = null;
        _receiveChannel?.Writer.Complete();
        _receiveChannel = null;
        await TaskUtility.TryWhenAll(_processTasks);
        _processTasks = [];
        _client?.Dispose();
        _client = null;
        _peer.Dispose();
        await base.DisposeAsyncCore();
    }

    private static async Task ProcessSendAsync(
        UdpClient client, Channel<MessageRequest> channel, CancellationToken cancellationToken)
    {
        var requestReader = channel.Reader;
        try
        {
            await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
            {
                // Trace.WriteLine("Send request: " + request.Identity);
                var messageEnvelope = request.MessageEnvelope;
                var receiver = request.Receiver;
                var bytes = ModelSerializer.SerializeToBytes(messageEnvelope);
                var r = await client.SendAsync(bytes, bytes.Length, receiver.EndPoint.Host, receiver.EndPoint.Port);
                Trace.WriteLine($"Sent <{messageEnvelope.Message.GetType().Name}>: {messageEnvelope.Identity}, {receiver.EndPoint.Port}, {r}/{bytes.Length}");
            }
        }
        catch
        {
            // do nothing
        }
    }

    private static async Task ProcessReceiveAsync(
        UdpClient client, Channel<MessageEnvelope> receiveChannel, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync(cancellationToken);
                var messageEnvelope = ModelSerializer.DeserializeFromBytes<MessageEnvelope>(result.Buffer);
                Trace.WriteLine($"Received <{messageEnvelope.Message.GetType().Name}>: {messageEnvelope.Identity}");
                receiveChannel.Writer.TryWrite(messageEnvelope);
                // messageRouter.Handle(messageEnvelope);
                await Task.Yield();
            }
        }
        catch
        {
            // do nothing
        }
    }

    private static async Task ProcessHandleAsync(
        Channel<MessageEnvelope> channel, MessageRouter messageRouter, CancellationToken cancellationToken)
    {
        var reader = channel.Reader;
        try
        {
            await foreach (var messageEnvelope in reader.ReadAllAsync(cancellationToken))
            {
                messageRouter.Handle(messageEnvelope);
                Trace.WriteLine($"Handled <{messageEnvelope.Message.GetType().Name}>: {messageEnvelope.Identity}");
            }
        }
        catch
        {
            // do nothing
        }
    }

    private sealed record class MessageRequest
    {
        public required MessageEnvelope MessageEnvelope { get; init; }

        public required Peer Receiver { get; init; }
    }
}
