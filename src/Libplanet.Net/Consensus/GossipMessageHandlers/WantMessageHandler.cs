using System.Collections.Concurrent;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.GossipMessageHandlers;

internal sealed class WantMessageHandler(
    ConcurrentDictionary<MessageId, IMessage> messageById,
    ConcurrentDictionary<Peer, HashSet<MessageId>> haveDict)
    : MessageHandlerBase<HaveMessage>
{
    protected override void OnHandle(HaveMessage message, MessageEnvelope messageEnvelope)
    {
        // var messages = wantMessage.Ids.Select(id => messageById[id]).ToArray();

        // Parallel.ForEach(messages, Invoke);

        // void Invoke(IMessage message)
        // {
        //     try
        //     {
        //         // _validateSendingMessageSubject.OnNext(message);
        //         _ = replyContext.NextAsync(message);
        //     }
        //     catch (Exception)
        //     {
        //         // do nothing
        //     }
        // }
    }
}
