using System.Text.Json;
using System.Text.Json.Serialization;
using Libplanet.Types.Evidence;

namespace Libplanet.Types.JsonConverters;

internal class EvidenceIdJsonConverter : JsonConverter<EvidenceId>
{
    public override EvidenceId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        string hex = reader.GetString() ?? throw new JsonException("Expected a string.");
        try
        {
            return EvidenceId.Parse(hex);
        }
        catch (ArgumentException e)
        {
            throw new JsonException(e.Message);
        }
    }

    public override void Write(
        Utf8JsonWriter writer, EvidenceId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
