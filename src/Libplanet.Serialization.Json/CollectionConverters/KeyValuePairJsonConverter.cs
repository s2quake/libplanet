using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class KeyValuePairJsonConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert) => IsKeyValuePair(typeToConvert);

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.ReadStartObject();
        var keyType = typeToConvert.GetGenericArguments()[0];
        var key = reader.ReadObject("Key", keyType, options);
        var valueType = typeToConvert.GetGenericArguments()[1];
        var value = reader.ReadObject("Value", valueType, options);
        reader.Expect(JsonTokenType.EndObject);
        return TypeUtility.CreateInstance(typeToConvert, args: [key, value]);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        var type = value.GetType();
        if (type.GetProperty(nameof(KeyValuePair<string, string>.Key)) is not { } keyProperty)
        {
            throw new UnreachableException("Key property not found");
        }

        if (type.GetProperty(nameof(KeyValuePair<string, string>.Value)) is not { } valueProperty)
        {
            throw new UnreachableException("Value property not found");
        }

        writer.WriteStartObject();
        writer.WritePropertyName("Key");
        JsonSerializer.Serialize(writer, keyProperty.GetValue(value), keyProperty.PropertyType, options);
        writer.WritePropertyName("Value");
        JsonSerializer.Serialize(writer, valueProperty.GetValue(value), valueProperty.PropertyType, options);
        writer.WriteEndObject();
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
