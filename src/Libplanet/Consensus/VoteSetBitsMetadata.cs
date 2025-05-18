using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Consensus;

public sealed record class VoteSetBitsMetadata : IValidatableObject
{
    [NonNegative]
    public int Height { get; init; }

    [NonNegative]
    public int Round { get; init; }

    public BlockHash BlockHash { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public Address Validator { get; init; }

    public VoteFlag Flag { get; init; }

    public ImmutableArray<bool> VoteBits { get; init; }

    public VoteSetBits Sign(PrivateKey signer)
    {
        var bytes = ModelSerializer.SerializeToBytes(this);
        var signature = signer.Sign(bytes).ToImmutableArray();
        return new VoteSetBits { Metadata = this, Signature = signature };
    }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Flag == VoteFlag.Null || Flag == VoteFlag.Unknown)
        {
            yield return new ValidationResult("Vote flag should be PreVote or PreCommit.", [nameof(Flag)]);
        }
    }
}
