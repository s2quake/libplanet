using System.Net;
using System.Net.Sockets;
using Libplanet.Types;

namespace Libplanet.Net;

internal sealed class TransportPeer : IDisposable
{
    private static readonly object _lock = new();
    private static readonly HashSet<int> _usedPorts = [];
    private readonly Peer _peer;
    private readonly int? _port;
    private bool _disposed;

    public TransportPeer(Address address, string host, int port)
    {
        if (port is 0)
        {
            port = GetPort();
            _port = port;
        }
        _peer = new Peer
        {
            Address = address,
            EndPoint = new DnsEndPoint(host, port),
        };
    }

    public string Host => _peer.EndPoint.Host;

    public int Port => _peer.EndPoint.Port;

    public static implicit operator Peer(TransportPeer transportPeer) => transportPeer._peer;

    private static int GetPort()
    {
        lock (_lock)
        {
            int port;
            do
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
            }
            while (_usedPorts.Contains(port));
            _usedPorts.Add(port);
            return port;
        }
    }

    private static void ReleasePort(int port)
    {
        lock (_lock)
        {
            _usedPorts.Remove(port);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_port is not null)
            {
                ReleasePort(_port.Value);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
