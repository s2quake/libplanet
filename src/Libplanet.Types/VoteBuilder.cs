namespace Libplanet.Types;

public sealed record class VoteBuilder
{
    public required Validator Validator { get; init; }

    public required Block Block { get; init; }

    public int Round { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public VoteType Type { get; init; }

    public Vote Create(ISigner signer)
    {
        if (Type is not VoteType.PreVote and not VoteType.PreCommit)
        {
            throw new InvalidOperationException(
                $"The {nameof(Type)} must be either {VoteType.PreVote} or {VoteType.PreCommit}.");
        }

        var metadata = new VoteMetadata
        {
            Validator = Validator.Address,
            BlockHash = Block.BlockHash,
            Height = Block.Height,
            Round = Round,
            Timestamp = Timestamp == default ? DateTimeOffset.UtcNow : Timestamp,
            ValidatorPower = Validator.Power,
            Type = Type,
        };
        return metadata.Sign(signer);
    }
}
