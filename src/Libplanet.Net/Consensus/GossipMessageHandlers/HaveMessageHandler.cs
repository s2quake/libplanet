using System.Collections.Concurrent;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.GossipMessageHandlers;

internal sealed class HaveMessageHandler(
    ITransport transport,
    ConcurrentDictionary<MessageId, IMessage> messageById,
    ConcurrentDictionary<Peer, HashSet<MessageId>> haveDict)
    : MessageHandlerBase<HaveMessage>
{
    protected override void OnHandle(HaveMessage message, MessageEnvelope messageEnvelope)
    {
        var ids = message.Ids.Where(id => !messageById.ContainsKey(id)).ToArray();
        var peer = messageEnvelope.Sender;

        transport.Post(peer, new PongMessage(), messageEnvelope.Identity);
        if (ids.Length is 0)
        {
            return;
        }

        if (!haveDict.TryGetValue(peer, out HashSet<MessageId>? value))
        {
            value = [];
        }

        foreach (var id in ids)
        {
            value.Add(id);
        }

        haveDict[peer] = value;
    }
}
