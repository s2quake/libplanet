// using System.Diagnostics;
// using System.Net;
// using System.Net.Sockets;
// using System.Threading;
// using System.Threading.Channels;
// using System.Threading.Tasks;
// using Libplanet.Types;
// using Libplanet.Types.Threading;
// using NetMQ;
// using NetMQ.Sockets;

// namespace Libplanet.Net.NetMQ;

// internal sealed class NetMQSender(ISigner signer) : ServiceBase
// {
//     private static readonly object _lock = new();
//     private readonly Dictionary<Peer, PushSocket> _socketsByPeer = [];
//     private Task _processTask = Task.CompletedTask;
//     private Channel<MessageRequest>? _requestChannel;

//     public void Send(MessageRequest request)
//     {
//         if (_requestChannel is null)
//         {
//             throw new InvalidOperationException("Request channel is not initialized.");
//         }

//         if (!_requestChannel.Writer.TryWrite(request))
//         {
//             throw new InvalidOperationException("Failed to write request to the channel.");
//         }
//     }

//     protected override async Task OnStartAsync(CancellationToken cancellationToken)
//     {
//         var tcs = new TaskCompletionSource<Channel<MessageRequest>>();
//         cancellationToken.Register(() => tcs.TrySetCanceled());
//         _processTask = Task.Factory.StartNew(
//             () =>
//             {
//                 using var runtime = new NetMQRuntime();
//                 var requestChannel = Channel.CreateUnbounded<MessageRequest>();
//                 var task = RunRequestChannelAsync(requestChannel, StoppingToken);
//                 tcs.SetResult(requestChannel);
//                 runtime.Run(task);
//             },
//             StoppingToken,
//             TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
//             TaskScheduler.Default);
//         _requestChannel = await tcs.Task;
//     }

//     protected override async Task OnStopAsync(CancellationToken cancellationToken)
//     {
//         _requestChannel?.Writer.Complete();
//         _requestChannel = null;
//         await TaskUtility.TryWait(_processTask);
//         _processTask = Task.CompletedTask;
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

//     private PushSocket GetPushSocket(Peer receiver)
//     {
//         lock (_lock)
//         {
//             if (!_socketsByPeer.TryGetValue(receiver, out var socket))
//             {
//                 var address = GetAddress(receiver);
//                 socket = new PushSocket();
//                 socket.Connect(address);
//                 _socketsByPeer[receiver] = socket;
//             }

//             return socket;
//         }
//     }

//     private async Task RunRequestChannelAsync(Channel<MessageRequest> channel, CancellationToken cancellationToken)
//     {
//         var requestReader = channel.Reader;
//         try
//         {
//             await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
//             {
//                 Trace.WriteLine("Send request: " + request.Identity);
//                 var messageEnvelope = request.MessageEnvelope;
//                 var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
//                 var socket = GetPushSocket(request.Receiver);
//                 if (!socket.TrySendMultipartMessage(rawMessage))
//                 {
//                     throw new InvalidOperationException("Failed to send message to the dealer socket.");
//                 }

//                 Trace.WriteLine($"Sent message: {messageEnvelope.Identity}");
//             }
//         }
//         catch
//         {
//             // do nothing
//         }
//         finally
//         {
//             foreach (var (receiver, socket) in _socketsByPeer)
//             {
//                 var address = GetAddress(receiver);
//                 socket.Disconnect(address);
//                 socket.Dispose();
//             }

//             _socketsByPeer.Clear();
//         }
//     }
// }
