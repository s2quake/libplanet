using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Serialization;
using Libplanet.Types.Consensus;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockCommit : IEquatable<BlockCommit>, IValidatableObject
{
    // public BlockCommit(
    //     long height,
    //     int round,
    //     BlockHash blockHash,
    //     ImmutableArray<Vote> votes)
    // {
    //     // TODO: Implement separate exception for each case.
    //     // TODO: Optimize by using flags to allow single iterating through votes.
    //     if (height < 0)
    //     {
    //         throw new ArgumentOutOfRangeException(
    //             nameof(height),
    //             $"Height must be non-negative: {height}");
    //     }
    //     else if (round < 0)
    //     {
    //         throw new ArgumentOutOfRangeException(
    //             nameof(round),
    //             $"Round must be non-negative: {round}");
    //     }
    //     else if (votes.IsDefaultOrEmpty)
    //     {
    //         throw new ArgumentException("Empty set of votes is not allowed.", nameof(votes));
    //     }
    //     else if (votes.Any(vote =>
    //         vote.Height != height ||
    //         vote.Round != round ||
    //         !blockHash.Equals(vote.BlockHash) ||
    //         (vote.Flag != VoteFlag.Null && vote.Flag != VoteFlag.PreCommit) ||
    //         (vote.Flag == VoteFlag.PreCommit && !vote.Verify())))
    //     {
    //         throw new ArgumentException(
    //             $"Every vote must have the same height as {height}, the same round " +
    //             $"as {round}, the same hash as {blockHash}, and must have flag value of " +
    //             $"either {VoteFlag.Null} or {VoteFlag.PreCommit}, " +
    //             $"and must be signed if the vote's flag is {VoteFlag.PreCommit}.",
    //             nameof(votes));
    //     }

    //     Height = height;
    //     Round = round;
    //     BlockHash = blockHash;
    //     Votes = votes;
    // }

    [Property(0)]
    public long Height { get; init; }

    [Property(1)]
    public int Round { get; init; }

    [Property(2)]
    public BlockHash BlockHash { get; init; }

    [Property(3)]
    public ImmutableArray<Vote> Votes { get; init; }

    public bool Equals(BlockCommit? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public HashDigest<SHA256> ToHash()
        => HashDigest<SHA256>.DeriveFrom(ModelSerializer.SerializeToBytes(this));

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Height < 0)
        {
            yield return new ValidationResult(
                $"Height must be non-negative: {Height}", [nameof(Height)]);
        }

        if (Round < 0)
        {
            yield return new ValidationResult(
                $"Round must be non-negative: {Round}", [nameof(Round)]);
        }
        if (Votes.IsDefaultOrEmpty)
        {
            yield return new ValidationResult(
                "Empty set of votes is not allowed.", [nameof(Votes)]);
        }

        if (Votes.Any(vote =>
            vote.Height != Height ||
            vote.Round != Round ||
            !BlockHash.Equals(vote.BlockHash) ||
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
