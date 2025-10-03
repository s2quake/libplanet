using System.IO;
using System.Runtime.CompilerServices;

namespace Libplanet.Serialization.DynamicConverters;

internal sealed class TupleModelConverter : ModelConverterBase<object>, IModelComparer
{
    public override bool CanConvert(Type type) => IsTuple(type) || IsValueTupleType(type);

    public bool Equals(object obj1, object obj2, Type type)
    {
        var genericArguments = type.GetGenericArguments();
        if (obj1 is not ITuple tuple1 || obj2 is not ITuple tuple2)
        {
            return false;
        }

        if (genericArguments.Length != tuple1.Length || genericArguments.Length != tuple2.Length)
        {
            return false;
        }

        for (var i = 0; i < genericArguments.Length; i++)
        {
            var item1 = tuple1[i];
            var item2 = tuple2[i];
            if (!ModelResolver.Equals(item1, item2, genericArguments[i]))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(object obj, Type type)
    {
        var tuple = (ITuple)obj;
        var genericArguments = type.GetGenericArguments();
        HashCode hash = default;
        for (var i = 0; i < genericArguments.Length; i++)
        {
            var item = tuple[i];
            var itemType = genericArguments[i];
            hash.Add(ModelResolver.GetHashCode(item, itemType));
        }

        return hash.ToHashCode();
    }

    protected override object? Read(BinaryReader reader, Type type, ModelOptions options)
    {
        var genericArguments = type.GetGenericArguments();
        var values = new object?[genericArguments.Length];
        for (var i = 0; i < genericArguments.Length; i++)
        {
            var itemType = genericArguments[i];
            using var _ = ModelTypeScope.Push(itemType);
            values[i] = ModelSerializer.Deserialize(reader, itemType, options);
        }

        return TypeUtility.CreateInstance(type, args: values);
    }

    protected override void Write(BinaryWriter writer, object value, ModelOptions options)
    {
        var type = value.GetType();
        var genericArguments = type.GetGenericArguments();
        if (value is not ITuple tuple)
        {
            throw new ModelException(
                $"The value {value} is not a tuple of type {type}");
        }

        if (genericArguments.Length != tuple.Length)
        {
            throw new ModelException(
                $"The number of generic arguments {genericArguments.Length} does not match " +
                $"the number of tuple items {tuple.Length}");
        }

        for (var i = 0; i < genericArguments.Length; i++)
        {
            var itemType = genericArguments[i];
            var item = tuple[i];
            var itemActualType = TypeUtility.GetActualType(item, itemType);
            using var _ = ModelTypeScope.Push(itemType);
            ModelSerializer.Serialize(writer, item, itemActualType, options);
        }
    }

    private static bool IsTuple(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(Tuple<,>)
            || genericTypeDefinition == typeof(Tuple<,,>)
            || genericTypeDefinition == typeof(Tuple<,,,>)
            || genericTypeDefinition == typeof(Tuple<,,,,>)
            || genericTypeDefinition == typeof(Tuple<,,,,,>)
            || genericTypeDefinition == typeof(Tuple<,,,,,,>)
            || genericTypeDefinition == typeof(Tuple<,,,,,,,>);
    }

    private static bool IsValueTupleType(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(ValueTuple<>)
            || genericTypeDefinition == typeof(ValueTuple<,>)
            || genericTypeDefinition == typeof(ValueTuple<,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,,,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,,,,,>);
    }
}
