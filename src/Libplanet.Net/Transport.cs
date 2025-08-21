using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading.Channels;
using Libplanet.Net.Messages;
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
    private TcpListener? _listener;
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
        _listener = new TcpListener(Peer.EndPoint.Port);
        _listener.Start();
        _processTasks =
        [
            ProcessReceiveAsync(_listener, _receiveChannel, StoppingToken),
            ProcessSendAsync(_sendChannel, StoppingToken),
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
        _listener?.Stop();
        _listener?.Dispose();
        _listener = null;
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
        _listener?.Dispose();
        _listener = null;
        _peer.Dispose();
        await base.DisposeAsyncCore();
    }

    private static async Task ProcessSendAsync(
        Channel<MessageRequest> channel, CancellationToken cancellationToken)
    {
        var requestReader = channel.Reader;
        try
        {
            await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
            {
                var messageEnvelope = request.MessageEnvelope;
                var receiver = request.Receiver;
                var bytes = ModelSerializer.SerializeToBytes(messageEnvelope);
                var lengthBytes = BitConverter.GetBytes(bytes.Length);

                using var client = new TcpClient();
                try
                {
                    await client.ConnectAsync(receiver.EndPoint.Host, receiver.EndPoint.Port);
                    using var stream = client.GetStream();
                    await stream.WriteAsync(lengthBytes, cancellationToken);
                    await stream.WriteAsync(bytes, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
                catch (SocketException)
                {
                    // do nothing
                }
            }
        }
        catch
        {
            // do nothing
        }
    }

    private static async Task ProcessReceiveAsync(
        TcpListener listener, Channel<MessageEnvelope> receiveChannel, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = ReadMessageAsync(client, receiveChannel, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
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
                await messageRouter.HandleAsync(messageEnvelope, cancellationToken);
            }
        }
        catch
        {
            // do nothing
        }
    }

    private static async Task ReadMessageAsync(
        TcpClient client, Channel<MessageEnvelope> receiveChannel, CancellationToken cancellationToken)
    {
        using var _ = client;
        using var strema = client.GetStream();
        var lengthBuffer = new byte[sizeof(int)];
        var readLength = await ReadExactAsync(strema, lengthBuffer, sizeof(int), cancellationToken);
        if (readLength == 0)
        {
            return;
        }

        var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
        var messageBuffer = new byte[messageLength];
        readLength = await ReadExactAsync(strema, messageBuffer, messageLength, cancellationToken);
        if (readLength == 0)
        {
            return;
        }

        var messageEnvelope = ModelSerializer.DeserializeFromBytes<MessageEnvelope>(messageBuffer);
        receiveChannel.Writer.TryWrite(messageEnvelope);
    }

    private static async Task<int> ReadExactAsync(
        Stream stream, byte[] buffer, int size, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < size)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, size - offset), cancellationToken);
            if (read is 0)
            {
                return 0;
            }

            offset += read;
        }

        return offset;
    }

    private sealed record class MessageRequest
    {
        public required MessageEnvelope MessageEnvelope { get; init; }

        public required Peer Receiver { get; init; }
    }
}
