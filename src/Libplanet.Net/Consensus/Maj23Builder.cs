using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed record class Maj23Builder
{
    public required Validator Validator { get; init; }

    public required Block Block { get; init; }

    public int Round { get; init; }

    public VoteType VoteType { get; init; }

    public Maj23 Create(ISigner signer)
    {
        var metadata = new Maj23Metadata
        {
            Height = Block.Height,
            Round = Round,
            BlockHash = Block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = Validator.Address,
            VoteType = VoteType,
        };
        return metadata.Sign(signer);
    }
}
