using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed record class ConsensusPreVoteMessage : ConsensusVoteMessage, IValidatableObject
{
    [Property(0)]
    public required Vote PreVote { get; init; }

    public override MessageType Type => MessageType.ConsensusVote;

    public override BlockHash BlockHash => PreVote.BlockHash;

    public override VoteFlag Flag => PreVote.Flag;

    public override Address Validator => PreVote.Validator;

    public override int Height => PreVote.Height;

    public override int Round => PreVote.Round;

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (PreVote.Flag != VoteFlag.PreVote)
        {
            yield return new ValidationResult(
                $"Given {nameof(PreVote)}'s flag must be {VoteFlag.PreVote}.",
                [nameof(PreVote)]);
        }
    }
}
