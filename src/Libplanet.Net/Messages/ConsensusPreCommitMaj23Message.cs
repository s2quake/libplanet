using System.ComponentModel.DataAnnotations;
using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "ConsensusPreCommitMaj23Message")]
public sealed record class ConsensusPreCommitMaj23Message : ConsensusMessage, IValidatableObject
{
    [Property(0)]
    public required Maj23 Maj23 { get; init; }

    public override Address Validator => Maj23.Validator;

    public override int Height => Maj23.Height;

    public override int Round => Maj23.Round;

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Maj23.VoteType != VoteType.PreCommit)
        {
            yield return new ValidationResult(
                $"Given {nameof(Maj23)}'s flag must be {VoteType.PreCommit}.",
                [nameof(Maj23)]);
        }
    }
}
