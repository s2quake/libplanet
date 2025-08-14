using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusVoteBitsMessageHandler(Consensus _consensus, Gossip _gossip)
    : MessageHandlerBase<ConsensusVoteBitsMessage>
{
    protected override async ValueTask OnHandleAsync(ConsensusVoteBitsMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var voteBits = message.VoteBits;
        var consensus = _consensus;
        var bits = voteBits.Bits;
        if (consensus.Height == voteBits.Height)
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

            var sender = _gossip.Peers.First(peer => peer.Address.Equals(voteBits.Validator));
            _gossip.Broadcast([sender], [.. messageList]);
        }

        await ValueTask.CompletedTask;
    }
}
