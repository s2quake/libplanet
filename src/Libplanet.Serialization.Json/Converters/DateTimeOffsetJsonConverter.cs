using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.Converters;

public sealed class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() is string s
            ? DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : throw new JsonException("Expected a string.");

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("o", CultureInfo.InvariantCulture));
}
