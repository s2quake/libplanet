using System.Runtime.CompilerServices;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class TupleYamlTypeConverter : YamlTypeConverter<object>
{
    public override bool Accepts(Type type) => IsTuple(type) || IsValueTupleType(type);

    public override object? Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        parser.ReadStartArray();
        var genericArguments = type.GetGenericArguments();
        var values = new object?[genericArguments.Length];
        for (var i = 0; i < genericArguments.Length; i++)
        {
            var itemType = genericArguments[i];
            using var _ = ModelTypeScope.Push(itemType);
            var value = rootDeserializer(itemType);
            values[i] = value;
        }

        parser.ReadEndArray();
        return TypeUtility.CreateInstance(type, args: values);
    }

    public override void Write(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
    {
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

        emitter.WriteStartArray();
        for (var i = 0; i < genericArguments.Length; i++)
        {
            var itemType = genericArguments[i];
            var item = tuple[i];
            var itemActualType = TypeUtility.GetActualType(item, itemType);
            using var _ = ModelTypeScope.Push(itemType);
            serializer(item, itemActualType);
        }

        emitter.WriteEndArray();
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
