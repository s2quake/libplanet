using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "blkhd")]
public sealed partial record class BlockHeader : IValidatableObject
{
    public const int CurrentVersion = 0;

    [Property(0)]
    [NonNegative]
    [LessThanOrEqual(CurrentVersion)]
    public int Version { get; init; } = CurrentVersion;

    [Property(1)]
    [NonNegative]
    public required int Height { get; init; }

    [Property(2)]
    [NotDefault]
    public required DateTimeOffset Timestamp { get; init; }

    [Property(3)]
    [NotDefault]
    public required Address Proposer { get; init; }

    [Property(4)]
    public BlockHash PreviousBlockHash { get; init; }

    [Property(5)]
    public BlockCommit PreviousBlockCommit { get; init; }

    [Property(6)]
    public HashDigest<SHA256> PreviousStateRootHash { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PreviousBlockCommit != default)
        {
            if (PreviousBlockCommit.Height != Height - 1)
            {
                var message = $"{nameof(PreviousBlockCommit)}.{nameof(PreviousBlockCommit.Height)} " +
                              $"must be {Height - 1}.";
                yield return new ValidationResult(message, [nameof(PreviousBlockCommit)]);
            }

            if (PreviousBlockCommit.BlockHash != PreviousBlockHash)
            {
                var message = $"{nameof(PreviousBlockCommit)}.{nameof(PreviousBlockCommit.BlockHash)} " +
                              $"must be {PreviousBlockHash}.";
                yield return new ValidationResult(message, [nameof(PreviousBlockCommit)]);
            }
        }
    }
}
