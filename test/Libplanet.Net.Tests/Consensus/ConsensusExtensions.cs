using Libplanet.Net.Messages;

namespace Libplanet.Net.Tests.Consensus;

public static class ConsensusExtensions
{
    public static void ProduceMessage(this Net.Consensus.Consensus @this, ConsensusMessage consensusMessage)
    {
        if (consensusMessage is ConsensusPreVoteMessage preVoteMessage)
        {
            @this.Post(preVoteMessage.PreVote);
        }
        else if (consensusMessage is ConsensusPreCommitMessage preCommitMessage)
        {
            @this.Post(preCommitMessage.PreCommit);
        }
        else if (consensusMessage is ConsensusProposalMessage proposalMessage)
        {
            @this.Post(proposalMessage.Proposal);
        }
    }
}
