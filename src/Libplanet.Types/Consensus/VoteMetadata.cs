using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Consensus;

[Model(Version = 1)]
public sealed record class VoteMetadata : IValidatableObject
{
    [Property(0)]
    public required PublicKey ValidatorPublicKey { get; init; }

    [Property(1)]
    [NonNegative]
    public long Height { get; init; }

    [Property(2)]
    [NonNegative]
    public int Round { get; init; }

    [Property(3)]
    public BlockHash BlockHash { get; init; }

    [Property(4)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(5)]
    [Positive]
    public BigInteger ValidatorPower { get; init; }

    [Property(6)]
    public VoteFlag Flag { get; init; }

    public bool Verify(ImmutableArray<byte> signature)
    {
        return ValidatorPublicKey.Verify([.. ModelSerializer.SerializeToBytes(this)], signature);
    }

    public Vote Sign(PrivateKey signer)
    {
        var signature = signer.Sign(ModelSerializer.SerializeToBytes(this));
        return new Vote { Metadata = this, Signature = [.. signature] };
    }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (BlockHash.Equals(default) && (Flag == VoteFlag.Null || Flag == VoteFlag.Unknown))
        {
            yield return new ValidationResult(
                $"Given {nameof(BlockHash)} cannot be default if {nameof(Flag)} " +
                $"is {VoteFlag.Null} or {VoteFlag.Unknown}",
                [nameof(BlockHash)]);
        }
    }
}
