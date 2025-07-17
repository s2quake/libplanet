using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusMessageHandler(ConsensusReactor consensusReactor)
    : MessageHandlerBase<ConsensusMessage>
{
    protected override void OnHandle(ConsensusMessage message, MessageEnvelope messageEnvelope)
    {
        consensusReactor.HandleMessage(message);
    }
}
