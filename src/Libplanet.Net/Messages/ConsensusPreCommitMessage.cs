using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed record class ConsensusPreCommitMessage : ConsensusVoteMessage, IValidatableObject
{
    [Property(0)]
    public required Vote PreCommit { get; init; }

    public override MessageType Type => MessageType.ConsensusCommit;

    public override BlockHash BlockHash => PreCommit.BlockHash;

    public override VoteFlag Flag => PreCommit.Flag;

    public override Address Validator => PreCommit.Validator;

    public override int Height => PreCommit.Height;

    public override int Round => PreCommit.Round;

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (PreCommit.Flag != VoteFlag.PreCommit)
        {
            yield return new ValidationResult(
                $"Given {nameof(PreCommit)}'s flag must be {VoteFlag.PreCommit}.",
                [nameof(PreCommit)]);
        }
    }
}
