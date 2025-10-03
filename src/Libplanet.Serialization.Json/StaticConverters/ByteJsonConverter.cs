using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.StaticConverters;

internal sealed class ByteJsonConverter : JsonConverter<byte>
{
    public override byte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetByte();

    public override void Write(Utf8JsonWriter writer, byte value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}
