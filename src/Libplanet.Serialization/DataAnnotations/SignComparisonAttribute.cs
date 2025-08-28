using System.ComponentModel.DataAnnotations;

namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public abstract class SignComparisonAttribute : ComparisonAttribute
{
    protected SignComparisonAttribute()
        : base(0)
    {
    }

    private static readonly Type[] _supportedTypes =
    [
        typeof(sbyte),
        typeof(byte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
    ];

    protected override IComparable GetTargetComparable(Type valueType, ValidationContext validationContext)
    {
        try
        {
            return GetTargetComparable(valueType);
        }
        catch (NotSupportedException e)
        {
            var memberName = validationContext.DisplayName;
            var declaringType = validationContext.ObjectType;
            var message = $"The type {valueType.Name} of the property {memberName} of {declaringType.Name} " +
                          $"is not supported in {GetType().Name}. Supported types are: " +
                          string.Join(", ", _supportedTypes.Select(t => t.Name)) + ".";
            throw new NotSupportedException(message, e);
        }
    }

    private static IComparable GetTargetComparable(Type type) => type switch
    {
        Type t when t == typeof(sbyte) => (sbyte)0,
        Type t when t == typeof(byte) => (byte)0,
        Type t when t == typeof(short) => (short)0,
        Type t when t == typeof(ushort) => (ushort)0,
        Type t when t == typeof(int) => 0,
        Type t when t == typeof(uint) => 0u,
        Type t when t == typeof(long) => 0L,
        Type t when t == typeof(ulong) => 0uL,
        Type t when t == typeof(float) => 0f,
        Type t when t == typeof(double) => 0.0d,
        Type t when t == typeof(decimal) => 0m,
        Type t when t == typeof(BigInteger) => BigInteger.Zero,
        _ => throw new NotSupportedException(
            $"The type {type} is not supported. Supported types are: " +
            string.Join(", ", _supportedTypes.Select(t => t.Name)) + "."),
    };
}
