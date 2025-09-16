using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.Converters;

public sealed class Int64JsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetInt64();

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}
