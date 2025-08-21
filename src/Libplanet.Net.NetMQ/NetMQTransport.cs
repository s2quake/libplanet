using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Libplanet.Types.Threading;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;

namespace Libplanet.Net.NetMQ;

public sealed partial class NetMQTransport(ISigner signer, TransportOptions options)
    : ServiceBase(options.Logger), ITransport
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

    public override string Name => $"[{Peer}]";

    public MessageRouter MessageRouter { get; } = new MessageRouter();

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
               var logger = options.Logger;
               using var socket = new PullSocket();
               var task1 = ProcessReceiveAsync(socket, Peer, receiveChannel, MessageRouter, logger, StoppingToken);
               var task2 = ProcessSendAsync(signer, requestChannel, MessageRouter, logger, StoppingToken);
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
        PullSocket socket,
        Peer peer,
        Channel<MessageEnvelope> receiveChannel,
        MessageRouter messageRouter,
        ILogger<ITransport> logger,
        CancellationToken cancellationToken)
    {
        var address = $"tcp://{peer.EndPoint.Host}:{peer.EndPoint.Port}";
        socket.Bind(address);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var rawMessage = await socket.ReceiveMultipartMessageAsync(cancellationToken: cancellationToken);
                var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
                if (messageRouter.VerifyReceivedMessage(messageEnvelope))
                {
                    receiveChannel.Writer.TryWrite(messageEnvelope);
                    DebugMessageReceived(logger, messageEnvelope);
                }
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

    private static async Task ProcessSendAsync(
        ISigner signer,
        Channel<MessageRequest> channel,
        MessageRouter messageRouter,
        ILogger<ITransport> logger,
        CancellationToken cancellationToken)
    {
        var requestReader = channel.Reader;
        var _socketsByPeer = new Dictionary<Peer, PushSocket>();
        try
        {
            await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
            {
                var messageEnvelope = request.MessageEnvelope;
                if (messageRouter.VerifySendingMessagre(messageEnvelope))
                {
                    var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
                    var socket = GetPushSocket(request.Receiver);
                    if (!socket.TrySendMultipartMessage(rawMessage))
                    {
                        throw new InvalidOperationException("Failed to send message to the dealer socket.");
                    }

                    DebugMessageSent(logger, messageEnvelope, request.Receiver);
                }
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
                _ = messageRouter.HandleAsync(messageEnvelope, cancellationToken);
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
