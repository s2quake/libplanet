using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Components.MessageHandlers;

internal sealed class DefaultMessageHandler(PeerExplorer peerExplorer)
    : MessageHandlerBase<IMessage>
{
    protected override void OnHandle(IMessage message, MessageEnvelope messageEnvelope)
    {
        if (messageEnvelope.Sender != peerExplorer.Peer)
        {
            peerExplorer.AddOrUpdate(messageEnvelope.Sender);
        }
    }
}
