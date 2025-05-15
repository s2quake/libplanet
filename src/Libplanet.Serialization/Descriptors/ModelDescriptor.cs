using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ModelDescriptor : ModelDescriptorBase
{
    public override bool CanSerialize(Type type)
        => type.IsDefined(typeof(ModelAttribute));

    public override bool CanDeserialize(Type type)
        => type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(LegacyModelAttribute));

    public override object CreateInstance(Type type, IEnumerable<object?> values, ModelOptions options)
    {
        var obj = TypeUtility.CreateInstance(type);
        var i = 0;
        var enumerator = values.GetEnumerator();
        var propertyInfos = options.GetProperties(type);
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
            var originVersion = options.GetVersion(originType);
            var version = options.GetVersion(type);
            while (version < originVersion)
            {
                var args = new object[] { obj };
                type = options.GetType(originType, version + 1);
                obj = TypeUtility.CreateInstance(type, args: args);
                version++;
            }
        }

        return obj;
    }

    public override IEnumerable<Type> GetTypes(Type type, int length, ModelOptions options)
    {
        if (type.IsDefined(typeof(ModelAttribute)))
        {
            var propertyInfos = options.GetProperties(type);
            foreach (var propertyInfo in propertyInfos)
            {
                yield return propertyInfo.PropertyType;
            }
        }
        else if (type.GetCustomAttribute<LegacyModelAttribute>() is { } legacyModelAttribute)
        {
            var originType = legacyModelAttribute.OriginType;
            var originVersion = options.GetVersion(originType);
            var version = options.GetVersion(type);
            // var list = (List)value;
            // var obj = CreateInstance(type);
            var propertyInfos = options.GetProperties(type);
            foreach (var propertyInfo in propertyInfos)
            {
                yield return propertyInfo.PropertyType;
            }
        }
        else
        {
            throw new UnreachableException("ModelAttribute or LegacyModelAttribute is not found");
        }
    }

    public override IEnumerable<(Type Type, object? Value)> GetValues(object obj, Type type, ModelOptions options)
    {
        var propertyInfos = options.GetProperties(type);
        // var itemList = new List<IValue>(propertyInfos.Length);
        foreach (var propertyInfo in propertyInfos)
        {
            var item = propertyInfo.GetValue(obj);
            var itemType = propertyInfo.PropertyType;
            yield return (itemType, item);
            // var actualType = GetActualType(itemType, item);
            // var serialized = itemType != actualType
            //     ? Serialize(item) : SerializeRawValue(item, itemType, options);
            // itemList.Add(serialized);
        }
    }
}
