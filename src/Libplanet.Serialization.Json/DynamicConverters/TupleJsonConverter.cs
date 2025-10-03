using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class TupleJsonConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert) => IsTuple(typeToConvert) || IsValueTupleType(typeToConvert);

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var genericArguments = typeToConvert.GetGenericArguments();
        var values = new object?[genericArguments.Length];

        reader.Expect(JsonTokenType.StartArray);
        reader.Read();
        for (var i = 0; i < genericArguments.Length; i++)
        {
            var itemType = genericArguments[i];
            using var _ = ModelTypeScope.Push(itemType);
            var value = JsonSerializer.Deserialize(ref reader, itemType, options);
            values[i] = value;
            reader.Read();
        }

        reader.Expect(JsonTokenType.EndArray);

        return TypeUtility.CreateInstance(typeToConvert, args: values);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
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

        writer.WriteStartArray();
        for (var i = 0; i < genericArguments.Length; i++)
        {
            var itemType = genericArguments[i];
            var item = tuple[i];
            var itemActualType = TypeUtility.GetActualType(item, itemType);
            using var _ = ModelTypeScope.Push(itemType);
            JsonSerializer.Serialize(writer, item, itemActualType, options);
        }

        writer.WriteEndArray();
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
