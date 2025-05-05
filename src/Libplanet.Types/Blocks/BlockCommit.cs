using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Consensus;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockCommit : IEquatable<BlockCommit>, IValidatableObject
{
    public static BlockCommit Empty { get; } = new();

    [Property(0)]
    [NonNegative]
    public long Height { get; init; }

    [Property(1)]
    [NonNegative]
    public int Round { get; init; }

    [Property(2)]
    public BlockHash BlockHash { get; init; }

    [Property(3)]
    [NotDefault]
    [NotEmpty]
    public ImmutableArray<Vote> Votes { get; init; } = [];

    public bool Equals(BlockCommit? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public HashDigest<SHA256> ToHash()
        => HashDigest<SHA256>.DeriveFrom(ModelSerializer.SerializeToBytes(this));

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        var height = Height;
        var round = Round;
        var blockHash = BlockHash;

        if (Votes.Any(vote =>
            vote.Height != height ||
            vote.Round != round ||
            !blockHash.Equals(vote.BlockHash) ||
            (vote.Flag != VoteFlag.Null && vote.Flag != VoteFlag.PreCommit) ||
            (vote.Flag == VoteFlag.PreCommit && !vote.Verify())))
        {
            yield return new ValidationResult(
                $"Every vote must have the same height as {Height}, the same round " +
                $"as {Round}, the same hash as {BlockHash}, and must have flag value of " +
                $"either {VoteFlag.Null} or {VoteFlag.PreCommit}, " +
                $"and must be signed if the vote's flag is {VoteFlag.PreCommit}.",
                [nameof(Votes)]);
        }
    }
}
