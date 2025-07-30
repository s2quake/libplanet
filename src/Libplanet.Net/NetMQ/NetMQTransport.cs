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
    private static readonly object _lock = new();
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);
    private readonly ProtocolHash _protocolHash = options.Protocol.Hash;
    private Task _processTask = Task.CompletedTask;
    private Channel<MessageRequest>? _requestChannel;

    public NetMQTransport(ISigner signer)
        : this(signer, new TransportOptions())
    {
    }

    public MessageRouter MessageHandlers { get; } = [];

    public Peer Peer { get; } = CreatePeer(signer.Address, options.Host, options.Port);

    public Protocol Protocol => _options.Protocol;

    CancellationToken ITransport.StoppingToken => StoppingToken;

    IMessageRouter ITransport.MessageRouter => MessageHandlers;

    public MessageEnvelope Post(Peer receiver, IMessage message, Guid? replyTo)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!IsRunning || _requestChannel is null)
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

        if (!_requestChannel.Writer.TryWrite(messageRequest))
        {
            throw new InvalidOperationException("Failed to write request to the channel.");
        }

        return messageEnvelope;
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<Channel<MessageRequest>>();
        _processTask = Task.Factory.StartNew(
           () =>
           {
               using var runtime = new NetMQRuntime();
               var requestChannel = Channel.CreateUnbounded<MessageRequest>();
               using var socket = new PullSocket();
               var task1 = ReceiveAsync(socket, StoppingToken);
               var task2 = RunRequestChannelAsync(requestChannel, StoppingToken);
               tcs.SetResult(requestChannel);
               runtime.Run(task1, task2);
               int qwer = 0;
           },
           StoppingToken,
           TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
           TaskScheduler.Default);

        await Task.CompletedTask;
        _requestChannel = await tcs.Task;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _requestChannel?.Writer.Complete();
        _requestChannel = null;
        await TaskUtility.TryWait(_processTask);
        _processTask = Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _requestChannel?.Writer.Complete();
        _requestChannel = null;
        await base.DisposeAsyncCore();
        await TaskUtility.TryWait(_processTask);
        _processTask = Task.CompletedTask;
    }

    private async Task ReceiveAsync(PullSocket socket, CancellationToken cancellationToken)
    {
        
        var address = $"tcp://{Peer.EndPoint.Host}:{Peer.EndPoint.Port}";
        socket.Bind(address);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var rawMessage = await socket.ReceiveMultipartMessageAsync(cancellationToken: cancellationToken);
                var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
                Trace.WriteLine($"Received message: {messageEnvelope.Identity}");
                _ = Task.Run(() => MessageHandlers.Handle(messageEnvelope));
                await Task.Yield();
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

    private static string GetAddress(Peer peer)
    {
        var host = peer.EndPoint.Host;
        var port = peer.EndPoint.Port;
        var addresses = Dns.GetHostAddresses(host);
        var ipv4 = addresses.FirstOrDefault(addr => addr.AddressFamily is AddressFamily.InterNetwork)
            ?? throw new InvalidOperationException($"Failed to resolve for {host}");

        return $"tcp://{ipv4}:{port}";
    }

    private async Task RunRequestChannelAsync(Channel<MessageRequest> channel, CancellationToken cancellationToken)
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
}
