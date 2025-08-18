using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusVoteBitsMessageHandler(Consensus consensus, Gossip gossip)
    : MessageHandlerBase<ConsensusVoteBitsMessage>
{
    protected override async ValueTask OnHandleAsync(ConsensusVoteBitsMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var voteBits = message.VoteBits;
        var bits = voteBits.Bits;
        var sender = gossip.Peers.FirstOrDefault(peer => peer.Address.Equals(voteBits.Validator));
        if (sender is not null && consensus.Height == voteBits.Height)
        {
            var voteType = voteBits.VoteType;
            var votes = voteType == VoteType.PreVote
                ? consensus.Round.PreVotes.GetVotes(bits)
                : consensus.Round.PreCommits.GetVotes(bits);
            var messageList = new List<ConsensusMessage>();
            foreach (var vote in votes)
            {
                if (voteType == VoteType.PreVote)
                {
                    messageList.Add(new ConsensusPreVoteMessage { PreVote = vote });
                }
                else
                {
                    messageList.Add(new ConsensusPreCommitMessage { PreCommit = vote });
                }
            }

            gossip.Broadcast([sender], [.. messageList], messageEnvelope.Identity);
        }

        await ValueTask.CompletedTask;
    }
}
