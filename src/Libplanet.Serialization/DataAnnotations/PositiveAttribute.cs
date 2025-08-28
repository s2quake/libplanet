namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class PositiveAttribute : SignComparisonAttribute
{
    protected override bool Compare(IComparable value, IComparable target) => value.CompareTo(target) > 0;

    protected override string FormatErrorMessage(
        string memberName, Type declaringType, IComparable value, IComparable target)
        => $"The property {memberName} of {declaringType.Name} must be greater than zero. Current value: {value}.";
}
