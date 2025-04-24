using System.Text.Json;
using System.Text.Json.Serialization;
using Libplanet.Types.Tx;

namespace Libplanet.Types.JsonConverters;

internal sealed class TxIdJsonConverter : JsonConverter<TxId>
{
    public override TxId Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.GetString() is not { } hex)
        {
            throw new JsonException("Expected a string.");
        }

        try
        {
            return TxId.Parse(hex);
        }
        catch (ArgumentException e)
        {
            throw new JsonException(e.Message);
        }
    }

    public override void Write(Utf8JsonWriter writer, TxId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
