using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusProposalMessageHandler(Consensus consensus)
    : MessageHandlerBase<ConsensusProposalMessage>
{
    protected override async ValueTask OnHandleAsync(ConsensusProposalMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var proposal = message.Proposal;
        consensus.PostPropose(proposal);
        await ValueTask.CompletedTask;
    }
}
