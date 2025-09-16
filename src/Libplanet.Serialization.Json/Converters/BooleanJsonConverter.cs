using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.Converters;

public sealed class BooleanJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetBoolean();

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value);
}
