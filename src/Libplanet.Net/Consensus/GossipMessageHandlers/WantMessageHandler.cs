using System.Collections.Concurrent;
using System.Threading.Tasks;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.GossipMessageHandlers;

internal sealed class WantMessageHandler(
    ITransport transport,
    ConcurrentDictionary<MessageId, IMessage> messageById,
    ConcurrentDictionary<Peer, HashSet<MessageId>> haveDict)
    : MessageHandlerBase<HaveMessage>
{
    protected override void OnHandle(HaveMessage message, MessageEnvelope messageEnvelope)
    {
        // var peer = transport.Peer;
        // var messages = message.Ids.Select(id => messageById[id]).ToArray();

        // Parallel.ForEach(messages, Invoke);

        // void Invoke(IMessage message)
        // {
        //     try
        //     {
        //         // _validateSendingMessageSubject.OnNext(message);
        //         transport.Post(messageEnvelope.Sender, message, messageEnvelope.Identity);
        //     }
        //     catch (Exception)
        //     {
        //         // do nothing
        //     }
        // }
    }
}
