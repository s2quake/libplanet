using System.ComponentModel.DataAnnotations;
using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed record class ConsensusPreVoteMessage : ConsensusVoteMessage, IValidatableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsensusPreVoteMessage"/> class.
    /// </summary>
    /// <param name="vote">The <see cref="Vote"/> for <see cref="ConsensusStep.PreVote"/>
    /// to attach.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when given <paramref name="vote"/>'s
    /// <see cref="Vote.Flag"/> is not <see cref="VoteFlag.PreVote"/>.</exception>
    // public ConsensusPreVoteMsg(Vote vote)
    //     : base(vote.Validator, vote.Height, vote.Round, vote.BlockHash, vote.Flag)
    // {
    //     if (vote.Flag != VoteFlag.PreVote)
    //     {
    //         throw new ArgumentException(
    //             $"Given {nameof(vote)}'s flag must be {VoteFlag.PreVote}.", nameof(vote));
    //     }

    //     PreVote = vote;
    // }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsensusPreVoteMsg"/> class
    /// with marshalled message.
    /// </summary>
    /// <param name="dataframes">A marshalled message.</param>
    // public ConsensusPreVoteMsg(byte[][] dataframes)
    //     : this(ModelSerializer.DeserializeFromBytes<Vote>(dataframes[0]))
    // {
    // }

    [Property(0)]
    public required Vote PreVote { get; init; }

    /// <inheritdoc cref="MessageContent.DataFrames"/>
    // public override IEnumerable<byte[]> DataFrames =>
    //     new List<byte[]> { ModelSerializer.SerializeToBytes(PreVote) };

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

    // public override bool Equals(ConsensusMessage? other)
    // {
    //     return other is ConsensusPreVoteMsg message &&
    //         PreVote.Equals(message.PreVote);
    // }

    // public override bool Equals(object? obj)
    // {
    //     return obj is ConsensusMessage other && Equals(other);
    // }

    // public override int GetHashCode()
    // {
    //     return HashCode.Combine(Type, PreVote);
    // }
}
