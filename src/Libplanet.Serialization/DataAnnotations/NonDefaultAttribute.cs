using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NonDefaultAttribute : ValidationAttribute
{
    private static readonly ConcurrentDictionary<Type, object?> _defaultValueByType = new();

    public override bool IsValid(object? value)
    {
        if (value is not null && value.GetType() is { } type && type.IsValueType)
        {
            var defaultValue = _defaultValueByType.GetOrAdd(type, Activator.CreateInstance);
            return Equals(value, defaultValue) is false;
        }

        return false;
    }
}
