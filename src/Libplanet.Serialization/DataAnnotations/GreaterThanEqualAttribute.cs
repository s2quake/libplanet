namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class GreaterThanEqualAttribute : ComparisonAttribute
{
    public GreaterThanEqualAttribute(Type? targetType, string propertyName)
        : base(targetType, propertyName)
    {
    }

    public GreaterThanEqualAttribute(object value)
        : base(value)
    {
    }

    public GreaterThanEqualAttribute(string textValue, Type valueType)
        : base(textValue, valueType)
    {
    }

    protected override bool Compare(IComparable value, IComparable target)
        => value.CompareTo(target) >= 0;

    protected override string FormatErrorMessage(
        string memberName, Type declaringType, IComparable value, IComparable target)
        => $"The property {memberName} of {declaringType.Name} must be greater than or equal to {target}. " +
           $"Current value: {value}.";
}
