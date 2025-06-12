using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;
using NetMQ;
using NetMQ.Sockets;

namespace Libplanet.Net.Transports;

public static class BoundPeerExtensions
{
    public static Protocol QueryAppProtocolVersionNetMQ(
        this Peer peer,
        TimeSpan? timeout = null)
    {
        using var dealerSocket = new DealerSocket(ToNetMQAddress(peer));
        var privateKey = new PrivateKey();
        var ping = new PingMessage();
        var netMQMessageCodec = new NetMQMessageCodec();
        NetMQMessage request = netMQMessageCodec.Encode(
            new MessageEnvelope
            {
                Message = ping,
                Protocol = default,
                Remote = new Peer { Address = privateKey.Address, EndPoint = new DnsEndPoint("0.0.0.0", 0) },
                Timestamp = DateTimeOffset.UtcNow,
            },
            privateKey);

        TimeSpan timeoutNotNull = timeout ?? TimeSpan.FromSeconds(5);
        try
        {
            if (dealerSocket.TrySendMultipartMessage(timeoutNotNull, request))
            {
                var response = new NetMQMessage();
                if (dealerSocket.TryReceiveMultipartMessage(timeoutNotNull, ref response))
                {
                    return Protocol.FromToken(response.First.ConvertToString());
                }
            }
        }
        catch (TerminatingException)
        {
            throw new TimeoutException($"Peer didn't respond.");
        }

        throw new TimeoutException(
            $"Peer[{peer}] didn't respond within the specified time[{timeout}].");
    }

    internal static string ToNetMQAddress(this Peer peer)
    {
        return $"tcp://{peer.EndPoint.Host}:{peer.EndPoint.Port}";
    }

    internal static async Task<string> ResolveNetMQAddressAsync(this Peer peer, CancellationToken cancellationToken)
    {
        var host = peer.EndPoint.Host;
        var port = peer.EndPoint.Port;
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        var ipv4 = addresses.FirstOrDefault(addr => addr.AddressFamily is AddressFamily.InterNetwork)
            ?? throw new TransportException($"Failed to resolve for {host}");

        return $"tcp://{ipv4}:{port}";
    }
}
