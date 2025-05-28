using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Consensus;

[Model(Version = 1)]
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

    public VoteFlag Flag => Metadata.Flag;

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (!Metadata.Verify(Signature))
        {
            yield return new ValidationResult($"Given {nameof(Signature)} is invalid.", [nameof(Signature)]);
        }
    }
}
