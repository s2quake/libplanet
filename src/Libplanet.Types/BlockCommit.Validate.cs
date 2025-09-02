using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;

namespace Libplanet.Types;

public readonly partial record struct BlockCommit : IValidatableObject
{
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        var height = Height;
        var round = Round;
        var blockHash = BlockHash;

        if (Votes.Any(vote =>
            vote.Height != height ||
            vote.Round != round ||
            !blockHash.Equals(vote.BlockHash) ||
            (vote.Type != VoteType.Null && vote.Type != VoteType.PreCommit) ||
            (vote.Type == VoteType.PreCommit && !ModelValidationUtility.TryValidate(vote))))
        {
            yield return new ValidationResult(
                $"Every vote must have the same height as {Height}, the same round " +
                $"as {Round}, the same hash as {BlockHash}, and must have flag value of " +
                $"either {VoteType.Null} or {VoteType.PreCommit}, " +
                $"and must be signed if the vote's flag is {VoteType.PreCommit}.",
                [nameof(Votes)]);
        }
    }
}
