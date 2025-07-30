using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.NetMQ;
using Libplanet.Net.Options;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class Transport(ISigner signer, TransportOptions options) : ServiceBase, ITransport
{
    private static readonly object _lock = new();
    private readonly MessageRouter _messageRouter = new();
    private readonly ProtocolHash _protocolHash = options.Protocol.Hash;
    private Channel<MessageRequest>? _requestChannel;
    private UdpClient? _client;
    private Task[] _processTasks = [];

    IMessageRouter ITransport.MessageRouter => _messageRouter;

    public Peer Peer { get; } = CreatePeer(signer.Address, options.Host, options.Port);

    public Protocol Protocol => options.Protocol;

    CancellationToken ITransport.StoppingToken => StoppingToken;

    public MessageEnvelope Post(Peer receiver, IMessage message, Guid? replyTo)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_requestChannel is null)
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
            Lifespan = options.MessageLifetime,
            ReplyTimeout = options.ReplyTimeout,
        };
        var messageRequest = new MessageRequest
        {
            Receiver = receiver,
            MessageEnvelope = messageEnvelope,
        };

        _requestChannel.Writer.TryWrite(messageRequest);
        return messageEnvelope;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        _requestChannel = Channel.CreateUnbounded<MessageRequest>();
        _client = new UdpClient(Peer.EndPoint.Port);
        _processTasks =
        [
            ProcessReceiveAsync(_client, _messageRouter, StoppingToken),
            ProcessSendAsync(_client, _requestChannel, StoppingToken),
        ];
        return Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _requestChannel?.Writer.Complete();
        _requestChannel = null;
        await TaskUtility.TryWhenAll(_processTasks);
        _processTasks = [];
        _client?.Dispose();
        _client = null;
        await Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _requestChannel?.Writer.Complete();
        _requestChannel = null;
        await TaskUtility.TryWhenAll(_processTasks);
        _processTasks = [];
        _client?.Dispose();
        _client = null;
        await base.DisposeAsyncCore();
    }

    private static Peer CreatePeer(Address address, string host, int port)
    {
        return new Peer
        {
            Address = address,
            EndPoint = new DnsEndPoint(host, port is 0 ? GetRandomPort() : port),
        };
    }

    private static int GetRandomPort()
    {
        lock (_lock)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    private static async Task ProcessSendAsync(
        UdpClient client, Channel<MessageRequest> channel, CancellationToken cancellationToken)
    {
        var requestReader = channel.Reader;
        try
        {
            await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
            {
                Trace.WriteLine("Send request: " + request.Identity);
                var messageEnvelope = request.MessageEnvelope;
                var receiver = request.Receiver;
                var bytes = ModelSerializer.SerializeToBytes(messageEnvelope);
                _ = client.SendAsync(bytes, bytes.Length, receiver.EndPoint.Host, receiver.EndPoint.Port);

                Trace.WriteLine($"Sent message: {messageEnvelope.Identity}");
            }
        }
        catch
        {
            // do nothing
        }
    }

    private static async Task ProcessReceiveAsync(
        UdpClient client, MessageRouter messageRouter, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync(cancellationToken);
                var messageEnvelope = ModelSerializer.DeserializeFromBytes<MessageEnvelope>(result.Buffer);
                Trace.WriteLine($"Received message: {messageEnvelope.Identity}");
                messageRouter.Handle(messageEnvelope);
                await Task.Yield();
            }
        }
        catch
        {
            // do nothing
        }
    }
}
