// using System.Diagnostics;
// using System.Net;
// using System.Net.Sockets;
// using System.Reactive.Subjects;
// using System.Threading;
// using System.Threading.Tasks;
// using Libplanet.Net.Messages;
// using Libplanet.Types;
// using Libplanet.Types.Threading;
// using NetMQ;
// using NetMQ.Sockets;

// namespace Libplanet.Net.NetMQ;

// internal sealed class NetMQReceiver : ServiceBase
// {
//     private static readonly object _lock = new();
//     private readonly Subject<MessageEnvelope> _receivedSubject = new();
//     private readonly string _address;
//     private Task _processTask = Task.CompletedTask;

//     public NetMQReceiver(Address address, string host, int port)
//     {
//         Peer = new Peer
//         {
//             Address = address,
//             EndPoint = new DnsEndPoint(host, port is 0 ? GetRandomPort() : port),
//         };
//         _address = $"tcp://{Peer.EndPoint.Host}:{Peer.EndPoint.Port}";
//     }

//     public IObservable<MessageEnvelope> Received => _receivedSubject;

//     public Peer Peer { get; }

//     protected override async Task OnStartAsync(CancellationToken cancellationToken)
//     {
//         _processTask = Task.Factory.StartNew(
//            ProcessRuntime,
//            StoppingToken,
//            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
//            TaskScheduler.Default);

//         await Task.CompletedTask;
//     }

//     protected override async Task OnStopAsync(CancellationToken cancellationToken)
//     {
//         await TaskUtility.TryWait(_processTask);
//         _processTask = Task.CompletedTask;
//     }

//     protected override async ValueTask DisposeAsyncCore()
//     {
//         await base.DisposeAsyncCore();
//         await TaskUtility.TryWait(_processTask);
//     }

//     private static int GetRandomPort()
//     {
//         lock (_lock)
//         {
//             var listener = new TcpListener(IPAddress.Loopback, 0);
//             listener.Start();
//             var port = ((IPEndPoint)listener.LocalEndpoint).Port;
//             listener.Stop();
//             return port;
//         }
//     }

//     private async Task ReceiveAsync(CancellationToken cancellationToken)
//     {
//         using var socket = new PullSocket();
//         socket.Bind(_address);

//         try
//         {
//             while (!cancellationToken.IsCancellationRequested)
//             {
//                 var rawMessage = await socket.ReceiveMultipartMessageAsync(cancellationToken: cancellationToken);
//                 var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
//                 Trace.WriteLine($"Received message: {messageEnvelope.Identity}");
//                 _receivedSubject.OnNext(messageEnvelope);
//                 await Task.Yield();
//             }
//         }
//         catch
//         {
//             // do nothing
//         }
//         finally
//         {
//             socket.Unbind(_address);
//         }
//     }

//     private void ProcessRuntime()
//     {
//         using var runtime = new NetMQRuntime();
//         var task = ReceiveAsync(StoppingToken);
//         runtime.Run(task);
//     }
// }
