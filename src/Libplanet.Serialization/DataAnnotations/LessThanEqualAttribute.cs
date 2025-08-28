namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class LessThanEqualAttribute : ComparisonAttribute
{
    public LessThanEqualAttribute(Type? targetType, string propertyName)
        : base(targetType, propertyName)
    {
    }

    public LessThanEqualAttribute(object value)
        : base(value)
    {
    }

    protected override bool Compare(IComparable value, IComparable target)
        => value.CompareTo(target) <= 0;

    protected override string FormatErrorMessage(
        string memberName, Type declaringType, IComparable value, IComparable target)
        => $"The property {memberName} of {declaringType.Name} must be less than or equal to {target}. " +
           $"Current value: {value}.";
}
