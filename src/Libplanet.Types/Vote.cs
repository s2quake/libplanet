using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "Vote")]
public sealed partial record class Vote : IValidatableObject
{
    [Property(0)]
    public required VoteMetadata Metadata { get; init; }

    [Property(1)]
    [NotDefault]
    public ImmutableArray<byte> Signature { get; init; }

    public int Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Validator => Metadata.Validator;

    public BigInteger ValidatorPower => Metadata.ValidatorPower;

    public VoteType Type => Metadata.Type;

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Signature.Length > 0 && !Metadata.Verify(Signature.AsSpan()))
        {
            yield return new ValidationResult($"Given {nameof(Signature)} of Vote is invalid.", [nameof(Signature)]);
        }
    }
}
