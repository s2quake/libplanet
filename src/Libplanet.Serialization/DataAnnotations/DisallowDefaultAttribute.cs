using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Libplanet.Serialization.DataAnnotations;

/// <summary>
/// Specifies that a data field value cannot be the default value.
/// </summary>
/// <remarks>
/// This attribute is only valid for properties using struct types.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DisallowDefaultAttribute : ValidationAttribute
{
    private static readonly ConcurrentDictionary<Type, object> _defaultByType = [];

    public override bool IsValid(object? value)
    {
        if (value == null || !value.GetType().IsValueType)
        {
            return true;
        }

        var defaultValue = _defaultByType.GetOrAdd(value.GetType(), CreateDefault);
        return !Equals(value, defaultValue);
    }

    public override string FormatErrorMessage(string name)
        => $"{name} cannot have the default value.";

    private static object CreateDefault(Type type)
        => Activator.CreateInstance(type)
            ?? throw new UnreachableException("Failed to create default value.");
}
