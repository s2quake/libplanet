namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
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

    protected override bool Compare(IComparable value, IComparable target)
        => value.CompareTo(target) >= 0;

    protected override string FormatErrorMessage(string name, IComparable value, IComparable target)
        => $"The property {name} must be greater than or equal to {target}. " +
           $"Current value: {value}.";
}
