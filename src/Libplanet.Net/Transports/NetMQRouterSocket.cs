using System.Net;
using Libplanet.Types;
using NetMQ.Sockets;

namespace Libplanet.Net.Transports;

internal sealed class NetMQRouterSocket : RouterSocket
{
    public NetMQRouterSocket(Address address, string host, int port)
    {
        Peer = new Peer
        {
            Address = address,
            EndPoint = new DnsEndPoint(host, Bind(host, port)),
        };
    }

    public Peer Peer { get; }

    protected override void Dispose(bool disposing)
    {
        Unbind($"tcp://{Peer.EndPoint.Host}:{Peer.EndPoint.Port}");
        base.Dispose(disposing);
    }

    private int Bind(string host, int port)
    {
        if (port is 0)
        {
            return BindRandomPort($"tcp://{host}");
        }
        else
        {
            Bind($"tcp://{host}:{port}");
            return port;
        }
    }
}
