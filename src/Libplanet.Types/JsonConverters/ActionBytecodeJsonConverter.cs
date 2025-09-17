using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Types.JsonConverters;

internal sealed class ActionBytecodeJsonConverter : JsonConverter<ActionBytecode>
{
    public override ActionBytecode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.GetString() is not { } hex)
        {
            throw new JsonException("Expected a string.");
        }

        return new ActionBytecode(ByteUtility.ParseHex(hex));
    }

    public override void Write(Utf8JsonWriter writer, ActionBytecode value, JsonSerializerOptions options)
        => writer.WriteStringValue(ByteUtility.Hex(value.Bytes));
}
