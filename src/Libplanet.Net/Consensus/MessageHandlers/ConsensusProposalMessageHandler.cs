using System.Diagnostics;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusProposalMessageHandler(Consensus consensus, MessageCollection pendingMessages)
    : MessageHandlerBase<ConsensusProposalMessage>
{
    protected override async ValueTask OnHandleAsync(ConsensusProposalMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var proposal = message.Proposal;
        var height = consensus.Height;
        if (proposal.Height < height)
        {
            Trace.WriteLine($"{this.GetHashCode()} 1");
            throw new InvalidMessageException("Proposal height is lower than current consensus height");
        }
        else if (proposal.Height == height)
        {
            Trace.WriteLine($"{this.GetHashCode()} 2");
            await consensus.ProposeAsync(proposal, cancellationToken);
        }
        else
        {
            pendingMessages.Add(message);
        }
    }
}
