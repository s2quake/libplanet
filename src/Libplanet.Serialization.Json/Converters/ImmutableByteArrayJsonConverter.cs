using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.Converters;

internal sealed class ImmutableByteArrayJsonConverter : JsonConverter<ImmutableArray<byte>>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(ImmutableArray<byte>);

    public override ImmutableArray<byte> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.GetString() is not { } hex)
        {
            throw new JsonException("Expected a string.");
        }

        return ImmutableArray.Create(Convert.FromHexString(hex));
    }

    public override void Write(Utf8JsonWriter writer, ImmutableArray<byte> value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(Convert.ToHexString([.. value]).ToLowerInvariant());
    }
}
