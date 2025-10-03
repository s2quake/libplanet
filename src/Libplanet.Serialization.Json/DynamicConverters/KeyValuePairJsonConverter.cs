using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class KeyValuePairJsonConverter : JsonConverter<object>
{
    private static readonly string[] _names =
    [
        nameof(KeyValuePair<object, object>.Key),
        nameof(KeyValuePair<object, object>.Value),
    ];

    public override bool CanConvert(Type typeToConvert) => IsKeyValuePair(typeToConvert);

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var values = new object?[_names.Length];
        reader.Expect(JsonTokenType.StartArray);
        reader.Read();
        for (var i = 0; i < _names.Length; i++)
        {
            if (typeToConvert.GetProperty(_names[i]) is not { } property)
            {
                throw new UnreachableException($"{_names[i]} property not found");
            }

            var propertyType = property.PropertyType;
            using var _ = ModelTypeScope.Push(propertyType);
            values[i] = JsonSerializer.Deserialize(ref reader, propertyType, options);
            reader.Read();
        }

        reader.Expect(JsonTokenType.EndArray);
        return TypeUtility.CreateInstance(typeToConvert, args: values);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        var type = value.GetType();
        writer.WriteStartArray();
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
            JsonSerializer.Serialize(writer, propertyValue, propertyActualType, options);
        }

        writer.WriteEndArray();
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
