namespace Libplanet.Types;

public sealed record class VoteBuilder
{
    public required Validator Validator { get; init; }

    public BlockHash BlockHash { get; init; }

    public int Height { get; init; }

    public int Round { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public VoteType Type { get; init; }

    public Vote Create(ISigner signer)
    {
        var metadata = new VoteMetadata
        {
            Validator = Validator.Address,
            BlockHash = BlockHash,
            Height = Height,
            Round = Round,
            Timestamp = Timestamp,
            ValidatorPower = Validator.Power,
            Type = Type,
        };
        return metadata.Sign(signer);
    }
}
