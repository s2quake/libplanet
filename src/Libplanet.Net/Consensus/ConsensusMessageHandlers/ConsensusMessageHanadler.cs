using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusMessageHandler(ConsensusService consensusService)
    : MessageHandlerBase<ConsensusMessage>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        await consensusService.HandleMessageAsync(message, cancellationToken);
    }
}
