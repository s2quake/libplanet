namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
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

    public LessThanEqualAttribute(string textValue, Type valueType)
        : base(textValue, valueType)
    {
    }

    protected override bool Compare(IComparable value, IComparable target)
        => value.CompareTo(target) <= 0;

    protected override string FormatErrorMessage(
        string memberName, Type declaringType, IComparable value, IComparable target)
        => $"The property {memberName} of {declaringType.Name} must be less than or equal to {target}. " +
           $"Current value: {value}.";
}
