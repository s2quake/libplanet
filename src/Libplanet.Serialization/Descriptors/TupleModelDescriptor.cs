using System.Runtime.CompilerServices;

namespace Libplanet.Serialization.Descriptors;

internal sealed class TupleModelDescriptor : ModelDescriptor
{
    public override bool CanSerialize(Type type) => IsTuple(type) || IsValueTupleType(type);

    public override Type[] GetTypes(Type type, out bool isArray)
    {
        isArray = false;
        var genericArguments = type.GetGenericArguments();
        if (genericArguments.Length == 0)
        {
            throw new ModelSerializationException(
                $"The type {type} does not have any generic arguments");
        }

        return genericArguments;
    }

    public override object?[] Serialize(object obj, Type type, ModelOptions options)
    {
        var genericArguments = type.GetGenericArguments();
        if (obj is not ITuple tuple)
        {
            throw new ModelSerializationException(
                $"The value {obj} is not a tuple of type {type}");
        }

        if (genericArguments.Length != tuple.Length)
        {
            throw new ModelSerializationException(
                $"The number of generic arguments {genericArguments.Length} does not match " +
                $"the number of tuple items {tuple.Length}");
        }

        var values = new object?[genericArguments.Length];
        for (var i = 0; i < genericArguments.Length; i++)
        {
            values[i] = tuple[i];
        }

        return values;
    }

    public override object Deserialize(Type type, object?[] values, ModelOptions options)
        => TypeUtility.CreateInstance(type, args: values);

    public override bool Equals(object obj1, object obj2, Type type)
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

    public override int GetHashCode(object obj, Type type)
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
