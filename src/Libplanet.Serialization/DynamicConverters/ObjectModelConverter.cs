using System.IO;
using System.Reflection;

namespace Libplanet.Serialization.DynamicConverters;

internal sealed class ObjectModelConverter : ModelConverterBase<object>, IModelComparer
{
    public override bool CanConvert(Type type)
        => type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(OriginModelAttribute));

    public bool Equals(object obj1, object obj2, Type type)
    {
        var properties = ModelResolver.GetProperties(type);
        foreach (var property in properties)
        {
            var value1 = property.GetValue(obj1);
            var value2 = property.GetValue(obj2);
            if (!ModelResolver.Equals(value1, value2, property.PropertyType))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(object obj, Type type)
    {
        var properties = ModelResolver.GetProperties(type);
        HashCode hash = default;
        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            hash.Add(ModelResolver.GetHashCode(value, property.PropertyType));
        }

        return hash.ToHashCode();
    }

    protected override object? Read(BinaryReader reader, Type type, ModelOptions options)
    {
        var obj = TypeUtility.CreateInstance(type);
        var properties = ModelResolver.GetProperties(type);
        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            var propertyType = property.PropertyType;
            using var _ = ModelTypeScope.Push(property.PropertyType);
            var propertyValue = ModelSerializer.Deserialize(reader, propertyType, options);
            property.SetValue(obj, propertyValue);
        }

        if (type.GetCustomAttribute<OriginModelAttribute>() is { } originModelAttribute)
        {
            var originType = originModelAttribute.Type;
            var originVersion = ModelResolver.GetVersion(originType);
            var version = ModelResolver.GetVersion(type);
            while (version < originVersion)
            {
                var args = new object[] { obj };
                type = ModelResolver.GetType(originType, version + 1);
                obj = TypeUtility.CreateInstance(type, args: args);
                version++;
            }
        }

        if (options.IsValidationEnabled)
        {
            ModelResolver.Validate(obj, options);
        }

        return obj;
    }

    protected override void Write(BinaryWriter writer, object value, ModelOptions options)
    {
        var type = value.GetType();
        if (options.IsValidationEnabled && !TypeUtility.IsDefault(value, type))
        {
            ModelResolver.Validate(value, options);
        }

        if (type.GetCustomAttribute<OriginModelAttribute>() is { } originModelAttribute
            && !originModelAttribute.AllowSerialization)
        {
            var message = $"The type '{type}' is a legacy model and is not allowed to be serialized. " +
                          $"Because it is marked with 'OriginModelAttribute' with 'AllowSerialization = false'.";
            throw new ModelException(message);
        }

        var properties = ModelResolver.GetProperties(type);
        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            var propertyType = property.PropertyType;
            var propertyValue = property.GetValue(value);
            var propertyActualType = TypeUtility.GetActualType(propertyValue, propertyType);
            using var _ = ModelTypeScope.Push(propertyType);
            ModelSerializer.Serialize(writer, propertyValue, propertyActualType, options);
        }
    }
}
