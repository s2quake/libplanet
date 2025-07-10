// using System.Net;
// using System.Net.Sockets;
// using System.Text;
// using NetMQ.Sockets;

// namespace Libplanet.Net.Transports;

// internal sealed class NetMQDealerSocket : DealerSocket
// {
//     private static readonly object _lock = new();
//     private static readonly Dictionary<Peer, NetMQDealerSocket> _socketsByPeer = [];

//     private NetMQDealerSocket(Peer peer) => Peer = peer;

//     public Peer Peer { get; }

//     public static NetMQDealerSocket Create(Peer peer)
//     {
//         lock (_lock)
//         {
//             if (!_socketsByPeer.TryGetValue(peer, out var socket))
//             {
//                 var address = GetAddress(peer);
//                 socket = new NetMQDealerSocket(peer);
//                 socket.Options.DisableTimeWait = true;
//                 socket.Options.Identity = Encoding.UTF8.GetBytes(peer.ToString());
//                 socket.Connect(address);
//                 _socketsByPeer[peer] = socket;
//             }

//             return socket;
//         }
//     }

//     private static string GetAddress(Peer peer)
//     {
//         var host = peer.EndPoint.Host;
//         var port = peer.EndPoint.Port;
//         var addresses = Dns.GetHostAddresses(host);
//         var ipv4 = addresses.FirstOrDefault(addr => addr.AddressFamily is AddressFamily.InterNetwork)
//             ?? throw new InvalidOperationException($"Failed to resolve for {host}");

//         return $"tcp://{ipv4}:{port}";
//     }
// }
