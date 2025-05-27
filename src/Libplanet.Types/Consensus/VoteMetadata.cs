using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Consensus;

[Model(Version = 1)]
public sealed record class VoteMetadata : IValidatableObject
{
    [Property(0)]
    public required Address Validator { get; init; }

    [Property(1)]
    public HashDigest<SHA256> StateRootHash { get; init; }

    [Property(2)]
    [NonNegative]
    public int Height { get; init; }

    [Property(3)]
    [NonNegative]
    public int Round { get; init; }

    [Property(4)]
    public BlockHash BlockHash { get; init; }

    [Property(5)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(6)]
    [Positive]
    public BigInteger ValidatorPower { get; init; }

    [Property(7)]
    public VoteFlag Flag { get; init; }

    public bool Verify(ImmutableArray<byte> signature)
    {
        var message = ModelSerializer.SerializeToBytes(this);
        return Validator.Verify([.. message], signature);
    }

    public Vote Sign(PrivateKey signer)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var message = ModelSerializer.SerializeToBytes(this, options);
        var signature = signer.Sign(message);
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
