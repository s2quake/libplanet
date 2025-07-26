using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Components.PeerDiscoveryMessageHandlers;

internal sealed class DefaultMessageHandler(PeerDiscovery peerService)
    : MessageHandlerBase<IMessage>
{
    protected override void OnHandle(IMessage message, MessageEnvelope messageEnvelope)
    {
        if (messageEnvelope.Sender != peerService.Peer)
        {
            peerService.AddOrUpdate(messageEnvelope.Sender);
        }
    }
}
