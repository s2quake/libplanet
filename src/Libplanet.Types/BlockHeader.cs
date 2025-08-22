using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "BlockHeader")]
public sealed partial record class BlockHeader : IValidatableObject
{
    public const int CurrentProtocolVersion = 0;

    [Property(0)]
    [NonNegative]
    public int BlockVersion { get; init; } = CurrentProtocolVersion;

    [Property(1)]
    [NonNegative]
    public int Height { get; init; }

    [Property(2)]
    [NotDefault]
    public DateTimeOffset Timestamp { get; init; }

    [Property(3)]
    [NotDefault]
    public Address Proposer { get; init; }

    [Property(4)]
    public BlockHash PreviousHash { get; init; }

    [Property(5)]
    public BlockCommit PreviousCommit { get; init; }

    [Property(6)]
    public HashDigest<SHA256> PreviousStateRootHash { get; init; }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (BlockVersion > CurrentProtocolVersion)
        {
            yield return new ValidationResult(
                $"The version {BlockVersion} is not supported. " +
                $"The current protocol version is {CurrentProtocolVersion}.",
                [nameof(BlockVersion)]);
        }
    }
}
