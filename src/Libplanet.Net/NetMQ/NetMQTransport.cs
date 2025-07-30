using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace Libplanet.Net.NetMQ;

public sealed class NetMQTransport(ISigner signer, TransportOptions options)
    : ServiceBase, ITransport
{
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);
    private readonly ProtocolHash _protocolHash = options.Protocol.Hash;
    private readonly TransportPeer _peer = new(signer.Address, options.Host, options.Port);
    private Task _processTask = Task.CompletedTask;
    private Channel<MessageRequest>? _sendChannel;
    private Channel<MessageEnvelope>? _receiveChannel;

    public NetMQTransport(ISigner signer)
        : this(signer, new TransportOptions())
    {
    }

    public MessageRouter MessageRouter { get; } = [];

    public Peer Peer => _peer;

    public Protocol Protocol => _options.Protocol;

    CancellationToken ITransport.StoppingToken => StoppingToken;

    IMessageRouter ITransport.MessageRouter => MessageRouter;

    public MessageEnvelope Post(Peer receiver, IMessage message, Guid? replyTo)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!IsRunning || _sendChannel is null)
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

        if (!_sendChannel.Writer.TryWrite(messageRequest))
        {
            throw new InvalidOperationException("Failed to write request to the channel.");
        }

        return messageEnvelope;
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<(Channel<MessageRequest>, Channel<MessageEnvelope>)>();
        _processTask = Task.Factory.StartNew(
           async () =>
           {
               using var runtime = new NetMQRuntime();
               var requestChannel = Channel.CreateUnbounded<MessageRequest>();
               var receiveChannel = Channel.CreateUnbounded<MessageEnvelope>();
               using var socket = new PullSocket();
               var task1 = ProcessReceiveAsync(socket, Peer, receiveChannel, StoppingToken);
               var task2 = ProcessSendAsync(signer, requestChannel, StoppingToken);
               var task3 = ProcessHandleAsync(receiveChannel, MessageRouter, StoppingToken);
               tcs.SetResult((requestChannel, receiveChannel));
               runtime.Run(task1);
               await TaskUtility.TryWhenAll(task2, task3);
           },
           StoppingToken,
           TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
           TaskScheduler.Default);

        await Task.CompletedTask;
        (_sendChannel, _receiveChannel) = await tcs.Task;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _sendChannel?.Writer.Complete();
        _sendChannel = null;
        _receiveChannel?.Writer.Complete();
        _receiveChannel = null;
        await TaskUtility.TryWait(_processTask);
        _processTask = Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _sendChannel?.Writer.Complete();
        _sendChannel = null;
        _receiveChannel?.Writer.Complete();
        _receiveChannel = null;
        await TaskUtility.TryWait(_processTask);
        _processTask = Task.CompletedTask;
        _peer.Dispose();
        await base.DisposeAsyncCore();
    }

    private static string GetAddress(Peer peer)
    {
        var host = peer.EndPoint.Host;
        var port = peer.EndPoint.Port;
        var addresses = Dns.GetHostAddresses(host);
        var ipv4 = addresses.FirstOrDefault(addr => addr.AddressFamily is AddressFamily.InterNetwork)
            ?? throw new InvalidOperationException($"Failed to resolve for {host}");

        return $"tcp://{ipv4}:{port}";
    }

    private static async Task ProcessReceiveAsync(
        PullSocket socket, Peer peer, Channel<MessageEnvelope> receiveChannel, CancellationToken cancellationToken)
    {
        var address = $"tcp://{peer.EndPoint.Host}:{peer.EndPoint.Port}";
        socket.Bind(address);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var rawMessage = await socket.ReceiveMultipartMessageAsync(cancellationToken: cancellationToken);
                var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
                receiveChannel.Writer.TryWrite(messageEnvelope);
                Trace.WriteLine($"Received message: {messageEnvelope.Identity}");
            }
        }
        catch
        {
            // do nothing
        }
        finally
        {
            socket.Unbind(address);
        }
    }

    private static async Task ProcessSendAsync(ISigner signer, Channel<MessageRequest> channel, CancellationToken cancellationToken)
    {
        var requestReader = channel.Reader;
        var _socketsByPeer = new Dictionary<Peer, PushSocket>();
        try
        {
            await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
            {
                Trace.WriteLine("Send request: " + request.Identity);
                var messageEnvelope = request.MessageEnvelope;
                var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
                var socket = GetPushSocket(request.Receiver);
                if (!socket.TrySendMultipartMessage(rawMessage))
                {
                    throw new InvalidOperationException("Failed to send message to the dealer socket.");
                }

                Trace.WriteLine($"Sent message: {messageEnvelope.Identity}");
            }
        }
        catch
        {
            // do nothing
        }
        finally
        {
            foreach (var (receiver, socket) in _socketsByPeer)
            {
                var address = GetAddress(receiver);
                socket.Disconnect(address);
                socket.Dispose();
            }

            _socketsByPeer.Clear();
        }

        PushSocket GetPushSocket(Peer receiver)
        {
            if (!_socketsByPeer.TryGetValue(receiver, out var socket))
            {
                var address = GetAddress(receiver);
                socket = new PushSocket();
                socket.Connect(address);
                _socketsByPeer[receiver] = socket;
            }

            return socket;
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

        public Guid Identity => MessageEnvelope.Identity;
    }
}
