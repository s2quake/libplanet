using System.Net;
using System.Net.Sockets;

namespace Libplanet.Net;

internal static class PortUtility
{
    private static readonly object _lock = new();
    private static readonly HashSet<int> _usedPorts = [];

    public static int GetPort()
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

    public static void ReleasePort(int port)
    {
        lock (_lock)
        {
            _usedPorts.Remove(port);
        }
    }
}
