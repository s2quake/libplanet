using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusProposalMessageHandler(Consensus consensus, MessageCollection pendingMessages)
    : MessageHandlerBase<ConsensusProposalMessage>
{
    protected override async ValueTask OnHandleAsync(ConsensusProposalMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var proposal = message.Proposal;
        if (proposal.Height < consensus.Height)
        {
            throw new InvalidMessageException("Proposal height is lower than current consensus height");
        }
        else if (proposal.Height == consensus.Height)
        {
            consensus.PostPropose(proposal);
        }
        else
        {
            pendingMessages.Add(message);
        }

        await ValueTask.CompletedTask;
    }
}
