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
        await consensusService.Dispatcher.InvokeAsync(_ =>
        {
            var consensus = consensusService.Consensus;
            VoteSetBits? voteSetBits = HandleMaj23(message.Maj23);
            if (voteSetBits is null)
            {
                return;
            }

            var sender = gossip.Peers.First(peer => peer.Address.Equals(message.Validator));
            gossip.PublishMessage(
                [sender],
                new ConsensusVoteSetBitsMessage { VoteSetBits = voteSetBits });
        }, cancellationToken);
    }

    private void Handle(Maj23 maj23)
    {
        var consensus = consensusService.Consensus;

        if (consensus.Height == maj23.Height && consensus.AddPreVoteMaj23(maj23))
        {
            var round = consensus.Rounds[maj23.Round];
            var votes = maj23.VoteType == VoteType.PreVote ? round.PreVotes : round.PreCommits;

            var voteBits = votes.GetVoteBits(maj23.BlockHash);
            var voteSetBits = new VoteSetBitsMetadata
            {
                Height = consensus.Height,
                Round = maj23.Round,
                BlockHash = maj23.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = maj23.Validator,
                VoteType = maj23.VoteType,
                VoteBits = [.. voteBits],
            }.Sign(signer);

            var validator = maj23.Validator;
            var sender = gossip.Peers.First(peer => peer.Address.Equals(validator));
            gossip.PublishMessage([sender], new ConsensusVoteSetBitsMessage { VoteSetBits = voteSetBits });
        }
    }

    public VoteSetBits? HandleMaj23(Maj23 maj23)
    {
        var consensus = consensusService.Consensus;
        var height = maj23.Height;
        if (height < consensus.Height)
        {
            // logging
        }
        else
        {
            if (consensus.Height == height && consensus.AddPreVoteMaj23(maj23))
            {
                return GetVoteSetBits(signer, consensus, maj23);
            }
        }

        return null;
    }

    private static VoteSetBits GetVoteSetBits(ISigner signer, Consensus consensus, Maj23 maj23)
    {
        if (maj23.Height != consensus.Height)
        {
            throw new ArgumentException(
                $"Maj23 height {maj23.Height} does not match expected height {consensus.Height}.", nameof(maj23));
        }

        // if (maj23.Round < 0 || maj23.Round >= _rounds.Count)
        // {
        //     throw new ArgumentOutOfRangeException(nameof(maj23), "Round is out of range.");
        // }

        var round = consensus.Rounds[maj23.Round];
        var votes = maj23.VoteType == VoteType.PreVote ? round.PreVotes : round.PreCommits;

        var voteBits = votes.GetVoteBits(maj23.BlockHash);
        return new VoteSetBitsMetadata
        {
            Height = consensus.Height,
            Round = maj23.Round,
            BlockHash = maj23.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = maj23.Validator,
            VoteType = maj23.VoteType,
            VoteBits = [.. voteBits],
        }.Sign(signer);
    }
}
