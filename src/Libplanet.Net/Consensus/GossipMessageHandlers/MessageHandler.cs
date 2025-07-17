using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.GossipMessageHandlers;

internal sealed class MessageHandler(Gossip gossip) : MessageHandlerBase<IMessage>
{
    protected override void OnHandle(IMessage message, MessageEnvelope messageEnvelope)
    {
        gossip.AddMessage(message);
    }
}
