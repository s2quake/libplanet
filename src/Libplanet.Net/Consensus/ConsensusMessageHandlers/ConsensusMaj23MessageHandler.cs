using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusMaj23MessageHandler(ISigner signer, ConsensusService consensusService, Gossip gossip)
    : MessageHandlerBase<ConsensusMaj23Message>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusMaj23Message message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        await consensusService.Dispatcher.InvokeAsync(_ => Handle(message.Maj23), cancellationToken);
    }

    private void Handle(Maj23 maj23)
    {
        var consensus = consensusService.Consensus;
        if (consensus.Height == maj23.Height && consensus.AddPreVoteMaj23(maj23))
        {
            var round = consensus.Rounds[maj23.Round];
            var votes = maj23.VoteType == VoteType.PreVote ? round.PreVotes : round.PreCommits;
            var voteBits = new VoteBitsMetadata
            {
                Height = consensus.Height,
                Round = maj23.Round,
                BlockHash = maj23.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = maj23.Validator,
                VoteType = maj23.VoteType,
                Bits = votes.GetBits(maj23.BlockHash),
            }.Sign(signer);

            var validator = maj23.Validator;
            var sender = gossip.Peers.First(peer => peer.Address.Equals(validator));
            gossip.PublishMessage([sender], new ConsensusVoteBitsMessage { VoteBits = voteBits });
        }
    }
}
