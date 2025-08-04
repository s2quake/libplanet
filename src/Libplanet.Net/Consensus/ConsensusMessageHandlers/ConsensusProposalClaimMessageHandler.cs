using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusProposalClaimMessageHandler(ConsensusService consensusService, Gossip gossip)
    : MessageHandlerBase<ConsensusProposalClaimMessage>
{
    protected override ValueTask OnHandleAsync(
        ConsensusProposalClaimMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        Proposal? proposal = consensusService.HandleProposalClaim(message.ProposalClaim);
        if (proposal is { } proposalNotNull)
        {
            var reply = new ConsensusProposalMessage { Proposal = proposalNotNull };
            var sender = gossip.Peers.First(
                peer => peer.Address.Equals(message.Validator));

            gossip.PublishMessage([sender], reply);
        }

        return ValueTask.CompletedTask;
    }
}
