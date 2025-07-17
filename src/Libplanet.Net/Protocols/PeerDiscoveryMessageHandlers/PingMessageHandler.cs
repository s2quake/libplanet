// using System.Threading;
// using System.Threading.Tasks;
// using Libplanet.Net.MessageHandlers;
// using Libplanet.Net.Messages;

// namespace Libplanet.Net.Protocols.PeerDiscoveryMessageHandlers;

// internal sealed class PingMessageHandler
//     : MessageHandlerBase<PingMessage>
// {
//     protected override void OnHandleAsync(
//         PingMessage message, MessageEnvelope messageEnvelope)
//     {
//         await replyContext.PongAsync();
//     }
// }
