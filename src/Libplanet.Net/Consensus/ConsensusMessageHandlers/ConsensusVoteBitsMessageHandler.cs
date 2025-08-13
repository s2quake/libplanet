using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusVoteBitsMessageHandler(ConsensusService consensusService, Gossip gossip)
    : MessageHandlerBase<ConsensusVoteBitsMessage>
{
    protected override ValueTask OnHandleAsync(
        ConsensusVoteBitsMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var messages = HandleVoteSetBits(message.VoteBits);
        var sender = gossip.Peers.First(peer => peer.Address.Equals(message.Validator));
        gossip.PublishMessage([sender], [.. messages]);
        return ValueTask.CompletedTask;
    }

    public IEnumerable<ConsensusMessage> HandleVoteSetBits(VoteBits voteSetBits)
    {
        var _consensus = consensusService.Consensus;
        var bits = voteSetBits.Bits;
        int height = voteSetBits.Height;
        // if (height < Height)
        // {
        //     // logging
        // }
        // else
        {
            if (_consensus.Height == height)
            {
                // NOTE: Should check if collected messages have same BlockHash with
                // VoteSetBit's BlockHash?
                var voteType = voteSetBits.VoteType;
                var votes = voteType == VoteType.PreVote
                    ? _consensus.Round.PreVotes.GetVotes(bits)
                    : _consensus.Round.PreCommits.GetVotes(bits);
                foreach (var vote in votes)
                {
                    yield return vote.Type switch
                    {
                        VoteType.PreVote => new ConsensusPreVoteMessage { PreVote = vote },
                        VoteType.PreCommit => new ConsensusPreCommitMessage { PreCommit = vote },
                        _ => throw new ArgumentException("VoteType should be PreVote or PreCommit.", nameof(vote)),
                    };
                }
            }
        }
    }
}
