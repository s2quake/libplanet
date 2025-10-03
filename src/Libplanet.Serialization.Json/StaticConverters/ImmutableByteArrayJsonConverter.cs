using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.StaticConverters;

internal sealed class ImmutableByteArrayJsonConverter : JsonConverter<ImmutableArray<byte>>
{
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
        => writer.WriteStringValue(Convert.ToHexString([.. value]).ToLowerInvariant());
}
