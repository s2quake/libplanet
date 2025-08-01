using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class HaveMessageHandler(MessageCollection messages, PeerMessageIdCollection peerMessageIds)
    : MessageHandlerBase<HaveMessage>
{
    protected override void OnHandle(HaveMessage message, MessageEnvelope messageEnvelope)
    {
        var ids = message.Ids.Where(id => !messages.Contains(id)).ToImmutableArray();
        var peer = messageEnvelope.Sender;

        if (ids.Length is 0)
        {
            return;
        }

        peerMessageIds.Add(peer, ids);
    }
}
