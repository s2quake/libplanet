using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class NullableJsonConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert) => Nullable.GetUnderlyingType(typeToConvert) is not null;

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        var underlyingType = Nullable.GetUnderlyingType(typeToConvert)
            ?? throw new UnreachableException($"{typeToConvert} is not a nullable type");
        return JsonSerializer.Deserialize(ref reader, underlyingType, options);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            var nullableType = value.GetType();
            JsonSerializer.Serialize(writer, value, nullableType, options);
        }
    }
}
