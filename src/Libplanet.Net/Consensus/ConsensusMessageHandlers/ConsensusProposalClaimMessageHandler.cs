using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusProposalClaimMessageHandler(ConsensusReactor consensusReactor, Gossip gossip)
    : MessageHandlerBase<ConsensusProposalClaimMessage>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusProposalClaimMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        Proposal? proposal = consensusReactor.HandleProposalClaim(message.ProposalClaim);
        if (proposal is { } proposalNotNull)
        {
            var reply = new ConsensusProposalMessage { Proposal = proposalNotNull };
            var sender = gossip.Peers.First(
                peer => peer.Address.Equals(message.Validator));

            gossip.PublishMessage([sender], reply);
        }

        await ValueTask.CompletedTask;
    }
}
