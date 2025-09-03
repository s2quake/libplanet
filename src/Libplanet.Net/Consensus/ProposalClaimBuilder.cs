using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed record class ProposalClaimBuilder
{
    public required Validator Validator { get; init; }

    public required Block Block { get; init; }

    public int Round { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public ProposalClaim Create(ISigner signer)
    {
        var metadata = new ProposalClaimMetadata
        {
            Validator = Validator.Address,
            BlockHash = Block.BlockHash,
            Height = Block.Height,
            Round = Round,
            Timestamp = Timestamp == default ? DateTimeOffset.UtcNow : Timestamp,
        };
        return metadata.Sign(signer);
    }
}
