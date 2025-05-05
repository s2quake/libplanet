using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotDefaultAttribute : ValidationAttribute
{
    private static readonly ConcurrentDictionary<Type, object?> _defaultValueByType = new();

    public override bool IsValid(object? value)
    {
        if (value?.GetType() is { IsValueType: true } type)
        {
            return !IsDefault(value, type);
        }

        return false;
    }

    internal static bool IsDefault(object value, Type type)
    {
        if (!type.IsValueType)
        {
            throw new ArgumentException(
                $"Type {type} is not a value type.", nameof(type));
        }

        var defaultValue = _defaultValueByType.GetOrAdd(type, Activator.CreateInstance);
        return Equals(value, defaultValue);
    }
}
