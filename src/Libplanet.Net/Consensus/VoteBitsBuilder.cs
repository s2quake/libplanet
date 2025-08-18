using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed record class VoteBitsBuilder
{
    public required Validator Validator { get; init; }

    public required Block Block { get; init; }

    public required ImmutableArray<bool> Bits { get; init; }

    public int Round { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public VoteType VoteType { get; init; }

    public VoteBits Create(ISigner signer)
    {
        var metadata = new VoteBitsMetadata
        {
            Validator = Validator.Address,
            BlockHash = Block.BlockHash,
            Height = Block.Height,
            Round = Round,
            Timestamp = Timestamp,
            VoteType = VoteType,
            Bits = Bits,
        };
        return metadata.Sign(signer);
    }
}
