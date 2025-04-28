namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class LessThanAttribute : ComparisonAttribute
{
    public LessThanAttribute(Type? targetType, string propertyName)
        : base(targetType, propertyName)
    {
    }

    public LessThanAttribute(object value)
        : base(value)
    {
    }

    protected override bool Compare(IComparable value, IComparable target)
        => value.CompareTo(target) < 0;

    protected override string FormatErrorMessage(string name, IComparable value, IComparable target)
        => $"The property {name} must be less than {target}. Current value: {value}.";
}
