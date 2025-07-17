using System.Collections.Concurrent;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.GossipMessageHandlers;

internal sealed class HaveMessageHandler(
    ConcurrentDictionary<MessageId, IMessage> messageById,
    ConcurrentDictionary<Peer, HashSet<MessageId>> haveDict)
    : MessageHandlerBase<HaveMessage>
{
    protected override void OnHandle(HaveMessage message, MessageEnvelope messageEnvelope)
    {
        var ids = message.Ids.Where(id => !messageById.ContainsKey(id)).ToArray();
        if (ids.Length is 0)
        {
            return;
        }

        var peer = messageEnvelope.Sender;
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
