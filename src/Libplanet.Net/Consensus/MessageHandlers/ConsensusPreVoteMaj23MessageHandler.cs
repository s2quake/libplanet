using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusPreVoteMaj23MessageHandler(ISigner signer, Consensus consensus, Gossip gossip)
    : MessageHandlerBase<ConsensusPreVoteMaj23Message>
{
    protected override async ValueTask OnHandleAsync(ConsensusPreVoteMaj23Message message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var maj23 = message.Maj23;
        if (consensus.Height == maj23.Height && consensus.AddPreVoteMaj23(maj23))
        {
            var round = consensus.Rounds[maj23.Round];
            var preVotes = round.PreVotes;
            var voteBits = new VoteBitsMetadata
            {
                Height = consensus.Height,
                Round = maj23.Round,
                BlockHash = maj23.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = maj23.Validator,
                VoteType = maj23.VoteType,
                Bits = preVotes.GetBits(maj23.BlockHash),
            }.Sign(signer);

            var validator = maj23.Validator;
            var sender = gossip.Peers.First(peer => peer.Address.Equals(validator));
            gossip.Broadcast([sender], new ConsensusVoteBitsMessage { VoteBits = voteBits });
        }

        await ValueTask.CompletedTask;
    }
}
