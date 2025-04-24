using System.ComponentModel.DataAnnotations;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Types.Evidence;

public abstract record class EvidenceBase
    : IEquatable<EvidenceBase>, IComparable<EvidenceBase>, IComparable, IValidatableObject
{
    private EvidenceId? _id;

    [Property(0)]
    public long Height { get; init; }

    [Property(1)]
    public Address TargetAddress { get; init; }

    [Property(2)]
    public DateTimeOffset Timestamp { get; init; }

    public EvidenceId Id => _id ??= new EvidenceId(ModelSerializer.SerializeToBytes(this));

    public int CompareTo(EvidenceBase? other) => Id.CompareTo(other?.Id);

    public int CompareTo(object? obj)
        => obj is EvidenceBase other ? CompareTo(other: other) : 1;

    public void Verify(IEvidenceContext evidenceContext) => OnVerify(evidenceContext);

    internal static string GetTypeName(Type evidenceType)
    {
        if (!typeof(EvidenceBase).IsAssignableFrom(evidenceType))
        {
            throw new ArgumentException(
                $"Given type {evidenceType} is not a subclass of {nameof(EvidenceBase)}.",
                nameof(evidenceType));
        }

        var typeName = evidenceType.FullName;
        var assemblyName = evidenceType.Assembly.GetName().Name;
        return $"{typeName}, {assemblyName}";
    }

    internal static string GetTypeName(EvidenceBase evidence)
        => GetTypeName(evidence.GetType());

    protected abstract void OnVerify(IEvidenceContext evidenceContext);

    protected virtual IEnumerable<ValidationResult> OnValidate(ValidationContext validationContext)
    {
        if (Height < 0)
        {
            yield return new ValidationResult(
                $"Given {nameof(Height)} cannot be negative: {Height}",
                [nameof(Height)]);
        }
    }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        => OnValidate(validationContext);
}
