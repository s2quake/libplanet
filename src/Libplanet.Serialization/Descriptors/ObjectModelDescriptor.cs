using System.Reflection;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ObjectModelDescriptor : ModelDescriptor
{
    public override bool CanSerialize(Type type)
        => type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(LegacyModelAttribute));

    public override object CreateInstance(Type type, IEnumerable<object?> values)
    {
        var obj = TypeUtility.CreateInstance(type);
        var i = 0;
        var enumerator = values.GetEnumerator();
        var propertyInfos = ModelResolver.GetProperties(type);
        while (enumerator.MoveNext())
        {
            var propertyInfo = type.GetProperties()[i];
            propertyInfo.SetValue(obj, enumerator.Current);
            i++;
        }

        if (i != propertyInfos.Length)
        {
            throw new ModelSerializationException(
                $"The number of values ({i}) does not match the number of properties ({propertyInfos.Length})");
        }

        if (type.GetCustomAttribute<LegacyModelAttribute>() is { } legacyModelAttribute)
        {
            var originType = legacyModelAttribute.OriginType;
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

        return obj;
    }

    public override IEnumerable<Type> GetTypes(Type type, int length)
    {
        if (type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(LegacyModelAttribute)))
        {
            var propertyInfos = ModelResolver.GetProperties(type);
            foreach (var propertyInfo in propertyInfos)
            {
                yield return propertyInfo.PropertyType;
            }
        }
    }

    public override IEnumerable<(Type Type, object? Value)> GetValues(object obj, Type type)
    {
        if (type.IsDefined(typeof(LegacyModelAttribute)))
        {
            throw new ModelSerializationException("LegacyModelAttribute is not supported");
        }

        var propertyInfos = ModelResolver.GetProperties(type);
        foreach (var propertyInfo in propertyInfos)
        {
            var item = propertyInfo.GetValue(obj);
            var itemType = propertyInfo.PropertyType;
            yield return (itemType, item);
        }
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
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
