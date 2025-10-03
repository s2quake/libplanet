using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class NullableJsonConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert) => Nullable.GetUnderlyingType(typeToConvert) is not null;

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (Nullable.GetUnderlyingType(ModelTypeScope.Current) is not { } underlyingType)
        {
            return JsonSerializer.Deserialize(ref reader, typeToConvert, options);
        }
        else if (Nullable.GetUnderlyingType(typeToConvert) == underlyingType)
        {
            using var _ = ModelTypeScope.Push(underlyingType);
            return JsonSerializer.Deserialize(ref reader, underlyingType, options);
        }
        else
        {
            throw new JsonException(
                $"Type '{typeToConvert}' is not a nullable type of '{underlyingType}'.");
        }
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (Nullable.GetUnderlyingType(ModelTypeScope.Current) is not { } underlyingType)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
        else if (value.GetType() == underlyingType)
        {
            using var _ = ModelTypeScope.Push(underlyingType);
            JsonSerializer.Serialize(writer, value, underlyingType, options);
        }
        else
        {
            throw new JsonException(
                $"Value type '{value.GetType()}' does not match the nullable type '{underlyingType}'.");
        }
    }
}
