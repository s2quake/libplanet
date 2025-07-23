using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusMessageHandler(ConsensusService consensusService)
    : MessageHandlerBase<ConsensusMessage>
{
    protected override void OnHandle(ConsensusMessage message, MessageEnvelope messageEnvelope)
    {
        consensusService.HandleMessage(message);
    }
}
