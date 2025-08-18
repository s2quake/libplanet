using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusProposalClaimMessageHandler(Consensus consensus, Gossip gossip)
    : MessageHandlerBase<ConsensusProposalClaimMessage>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusProposalClaimMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var proposalClaim = message.ProposalClaim;
        if (consensus.Height == proposalClaim.Height && consensus.Proposal is not null)
        {
            var reply = new ConsensusProposalMessage { Proposal = consensus.Proposal };
            var sender = gossip.Peers.First(
                peer => peer.Address.Equals(proposalClaim.Validator));

            gossip.Broadcast([sender], [reply], messageEnvelope.Identity);
        }

        await ValueTask.CompletedTask;
    }
}
