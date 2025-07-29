using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Threading;
using Libplanet.Types;
using NetMQ;
using NetMQ.Sockets;

namespace Libplanet.Net.NetMQ;

internal sealed class NetMQSender(ISigner signer, SynchronizationContext synchronizationContext)
    : IDisposable
{
    private static readonly object _lock = new();
    private readonly Dictionary<Peer, PushSocket> _socketsByPeer = [];
    private bool _disposed;

    public void Send(MessageRequest request)
    {
        if (synchronizationContext != SynchronizationContext.Current)
        {
            throw new InvalidOperationException(
                "Send method must be called from the same synchronization context as the one used to create this instance.");
        }

        if (request.Sender.Address != signer.Address)
        {
            throw new ArgumentException(
                $"The provided private key's address {signer.Address} does not match " +
                $"the remote peer's address {request.Sender.Address}.",
                nameof(request));
        }

        var messageEnvelope = request.MessageEnvelope;
        var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
        var socket = GetPushSocket(request.Sender, request.Receiver);
        if (!socket.TrySendMultipartMessage(rawMessage))
        {
            throw new InvalidOperationException("Failed to send message to the dealer socket.");
        }

        Trace.WriteLine($"Sent message: {messageEnvelope.Identity}");
    }


    // public async ValueTask DisposeAsync()
    // {
    //     if (!_disposed)
    //     {
    //         await synchronizationContext.PostAsync(() =>
    //         {
    //             foreach (var socket in _socketsByPeer.Values)
    //             {
    //                 socket.Dispose();
    //             }
    //         }, default);

    //         _disposed = true;
    //     }
    // }

    private PushSocket GetPushSocket(Peer sender, Peer receiver)
    {
        lock (_lock)
        {
            if (!_socketsByPeer.TryGetValue(receiver, out var socket))
            {
                var address = GetAddress(receiver);
                socket = new PushSocket();
                // socket.Options.DisableTimeWait = true;
                // socket.Options.Identity = Encoding.UTF8.GetBytes(sender.ToString());
                socket.Connect(address);
                _socketsByPeer[receiver] = socket;
            }

            return socket;
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

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var socket in _socketsByPeer.Values)
            {
                socket.Dispose();
            }

            _socketsByPeer.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
