using System.Diagnostics;
using System.IO;

namespace Libplanet.Serialization.DynamicConverters;

internal sealed class KeyValuePairModelConverter : ModelConverterBase<object>, IModelComparer
{
    private static readonly string[] _names =
    [
        nameof(KeyValuePair<object, object>.Key),
        nameof(KeyValuePair<object, object>.Value),
    ];

    public override bool CanConvert(Type type) => IsKeyValuePair(type);

    bool IModelComparer.Equals(object obj1, object obj2, Type type)
    {
        var propertyInfos = type.GetProperties();
        foreach (var propertyInfo in propertyInfos)
        {
            var value1 = propertyInfo.GetValue(obj1);
            var value2 = propertyInfo.GetValue(obj2);
            if (!ModelResolver.Equals(value1, value2, propertyInfo.PropertyType))
            {
                return false;
            }
        }

        return true;
    }

    int IModelComparer.GetHashCode(object obj, Type type)
    {
        var propertyInfos = type.GetProperties();
        HashCode hash = default;
        foreach (var propertyInfo in propertyInfos)
        {
            var value = propertyInfo.GetValue(obj);
            hash.Add(ModelResolver.GetHashCode(value, propertyInfo.PropertyType));
        }

        return hash.ToHashCode();
    }

    protected override object? Read(BinaryReader reader, Type type, ModelOptions options)
    {
        var values = new object?[_names.Length];
        for (var i = 0; i < _names.Length; i++)
        {
            if (type.GetProperty(_names[i]) is not { } property)
            {
                throw new UnreachableException($"{_names[i]} property not found");
            }

            var propertyType = property.PropertyType;
            using var _ = ModelTypeScope.Push(propertyType);
            values[i] = ModelSerializer.Deserialize(reader, propertyType, options);
        }

        return TypeUtility.CreateInstance(type, args: values);
    }

    protected override void Write(BinaryWriter writer, object value, ModelOptions options)
    {
        var type = value.GetType();
        for (var i = 0; i < _names.Length; i++)
        {
            if (type.GetProperty(_names[i]) is not { } property)
            {
                throw new UnreachableException($"{_names[i]} property not found");
            }

            var propertyType = property.PropertyType;
            var propertyValue = property.GetValue(value);
            var propertyActualType = TypeUtility.GetActualType(propertyValue, propertyType);
            using var _ = ModelTypeScope.Push(propertyType);
            ModelSerializer.Serialize(writer, propertyValue, propertyActualType, options);
        }
    }

    private static bool IsKeyValuePair(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(KeyValuePair<,>);
    }
}
