namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class GreaterThanAttribute : ComparisonAttribute
{
    public GreaterThanAttribute(Type? targetType, string propertyName)
        : base(targetType, propertyName)
    {
    }

    public GreaterThanAttribute(object value)
        : base(value)
    {
    }

    public GreaterThanAttribute(string textValue, Type valueType)
        : base(textValue, valueType)
    {
    }

    protected override bool Compare(IComparable value, IComparable target)
        => value.CompareTo(target) > 0;

    protected override string FormatErrorMessage(
        string memberName, Type declaringType, IComparable value, IComparable target)
        => $"The property {memberName} of {declaringType.Name} must be greater than {target}. Current value: {value}.";
}
