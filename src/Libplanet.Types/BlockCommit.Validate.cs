using System.ComponentModel.DataAnnotations;

namespace Libplanet.Types;

public readonly partial record struct BlockCommit : IValidatableObject
{
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        var height = Height;
        var round = Round;
        var blockHash = BlockHash;

        for (var i = 0; i < Votes.Length; i++)
        {
            var vote = Votes[i];
            if (vote.BlockHash != blockHash)
            {
                yield return new ValidationResult(
                    $"BlockHash of {nameof(Votes)}[{i}] is not equal to {blockHash}.",
                    [nameof(Votes)]);
            }

            if (vote.Height != height)
            {
                yield return new ValidationResult(
                    $"Height of {nameof(Votes)}[{i}] is not equal to {height}.",
                    [nameof(Votes)]);
            }

            if (vote.Round != round)
            {
                yield return new ValidationResult(
                    $"Round of {nameof(Votes)}[{i}] is not equal to {round}.",
                    [nameof(Votes)]);
            }

            if (vote.Type is not VoteType.Null and not VoteType.PreCommit)
            {
                yield return new ValidationResult(
                    $"Type of {nameof(Votes)}[{i}] must be either {VoteType.Null} or {VoteType.PreCommit}.",
                    [nameof(Votes)]);
            }

            if (vote.Type is VoteType.PreCommit && !vote.Verify())
            {
                yield return new ValidationResult(
                    $"The {i}-th vote in {nameof(Votes)} has an invalid signature.",
                    [nameof(Votes)]);
            }
        }
    }
}
