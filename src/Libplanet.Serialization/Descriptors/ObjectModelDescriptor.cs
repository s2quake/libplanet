using System.Reflection;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ObjectModelDescriptor : ModelDescriptor
{
    public override bool CanSerialize(Type type)
        => type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(OriginModelAttribute));

    public override Type[] GetTypes(Type type, out bool isArray)
    {
        isArray = false;
        var properties = ModelResolver.GetProperties(type);
        var types = new Type[properties.Length];
        for (var i = 0; i < properties.Length; i++)
        {
            types[i] = properties[i].PropertyType;
        }

        return types;
    }

    public override object?[] Serialize(object obj, Type type, ModelOptions options)
    {
        if (options.IsValidationEnabled)
        {
            ModelResolver.Validate(obj, options);
        }

        if (type.GetCustomAttribute<OriginModelAttribute>() is { } legacyModelAttribute
                && !legacyModelAttribute.AllowSerialization)
        {
            throw new ModelSerializationException("LegacyModelAttribute is not supported");
        }

        var properties = ModelResolver.GetProperties(type);
        var values = new object?[properties.Length];
        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            var value = property.GetValue(obj);
            values[i] = value;
        }

        return values;
    }

    public override object Deserialize(Type type, object?[] values, ModelOptions options)
    {
        var obj = TypeUtility.CreateInstance(type);
        var properties = ModelResolver.GetProperties(type);
        if (properties.Length != values.Length)
        {
            throw new ModelSerializationException(
                $"The number of properties ({properties.Length}) does not match the number of values ({values.Length})");
        }

        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            if (!property.ReadOnly)
            {
                var value = values[i];
                property.SetValue(obj, value);
            }
        }

        if (type.GetCustomAttribute<OriginModelAttribute>() is { } legacyModelAttribute)
        {
            var originType = legacyModelAttribute.Type;
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

    public override bool Equals(object obj1, object obj2, Type type)
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

    public override int GetHashCode(object obj, Type type)
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
}
