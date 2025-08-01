using System.Collections.Concurrent;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.GossipMessageHandlers;

internal sealed class HaveMessageHandler(
    ITransport transport,
    MessageCollection messages,
    PeerMessageIdCollection haveDict)
    : MessageHandlerBase<HaveMessage>
{
    protected override void OnHandle(HaveMessage message, MessageEnvelope messageEnvelope)
    {
        var ids = message.Ids.Where(id => !messages.Contains(id)).ToArray();
        var peer = messageEnvelope.Sender;

        transport.Post(peer, new PongMessage(), messageEnvelope.Identity);
        if (ids.Length is 0)
        {
            return;
        }

        haveDict.Add(peer, [..ids]);
    }
}
