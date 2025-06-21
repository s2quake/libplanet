using System.Reflection;

namespace Libplanet.TestUtilities;

public sealed class PropertyScope : IDisposable
{
    private const BindingFlags _bindingFlags = BindingFlags.Static | BindingFlags.Public;
    private readonly PropertyInfo _propertyInfo;
    private readonly object? _originalValue;

    public PropertyScope(Type type, string propertyName, object value)
    {
        if (type.GetProperty(propertyName, _bindingFlags) is not PropertyInfo property)
        {
            throw new ArgumentException(
                $"Property '{propertyName}' does not exist on type '{type.FullName}'.",
                nameof(propertyName));
        }

        if (!property.CanWrite)
        {
            throw new InvalidOperationException(
                $"Property '{propertyName}' on type '{type.FullName}' is read-only.");
        }

        _propertyInfo = property;
        _originalValue = property.GetValue(null);
        _propertyInfo.SetValue(null, value);
    }

    public void Dispose() => _propertyInfo.SetValue(null, _originalValue);
}