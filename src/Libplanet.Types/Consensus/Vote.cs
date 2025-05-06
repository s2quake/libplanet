using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Consensus;

[Model(Version = 1)]
public sealed record class Vote : IEquatable<Vote>, IValidatableObject
{
    [Property(0)]
    public required VoteMetadata Metadata { get; init; }

    [Property(1)]
    [NotDefault]
    public ImmutableArray<byte> Signature { get; init; }

    public long Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public PublicKey ValidatorPublicKey => Metadata.ValidatorPublicKey;

    public BigInteger ValidatorPower => Metadata.ValidatorPower;

    public VoteFlag Flag => Metadata.Flag;

    public bool Equals(Vote? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (!Metadata.Verify(Signature))
        {
            yield return new ValidationResult($"Given {nameof(Signature)} is invalid.", [nameof(Signature)]);
        }
    }
}
