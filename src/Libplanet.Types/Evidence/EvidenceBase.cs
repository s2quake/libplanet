using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Evidence;

public abstract record class EvidenceBase
    : IEquatable<EvidenceBase>, IComparable<EvidenceBase>, IComparable, IValidatableObject
{
    private EvidenceId? _id;

    [Property(0)]
    [NonNegative]
    public long Height { get; init; }

    [Property(1)]
    [NotDefault]
    public Address TargetAddress { get; init; }

    [Property(2)]
    public DateTimeOffset Timestamp { get; init; }

    public EvidenceId Id => _id ??= new EvidenceId(SHA256.HashData(ModelSerializer.SerializeToBytes(this)));

    public int CompareTo(EvidenceBase? other) => Id.CompareTo(other?.Id);

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        EvidenceBase other => CompareTo(other),
        _ => throw new ArgumentException($"Argument {nameof(obj)} is not ${nameof(EvidenceBase)}.", nameof(obj)),
    };

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        => OnValidate(validationContext);

    protected virtual IEnumerable<ValidationResult> OnValidate(ValidationContext validationContext)
    {
        yield break;
    }
}
