using System.Reflection;

namespace Libplanet.Serialization;

public sealed class ModelProperty
{
    private readonly PropertyAttribute _propertyAttribute;
    private readonly PropertyInfo _propertyInfo;

    internal ModelProperty(PropertyAttribute propertyAttribute, PropertyInfo propertyInfo)
    {
        _propertyAttribute = propertyAttribute;
        _propertyInfo = propertyInfo;
    }

    public bool EmitDefaultValue => _propertyAttribute.EmitDefaultValue;

    public Type PropertyType => _propertyInfo.PropertyType;

    public string Name => _propertyInfo.Name;

    public object? GetValue(object obj) => _propertyInfo.GetValue(obj);

    public void SetValue(object obj, object? value) => _propertyInfo.SetValue(obj, value);
}
