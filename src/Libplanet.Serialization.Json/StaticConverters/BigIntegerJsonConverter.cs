using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.StaticConverters;

internal sealed class BigIntegerJsonConverter : JsonConverter<BigInteger>
{
    public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Number)
        {
            return new BigInteger(reader.GetInt64());
        }
        else if (reader.TokenType is JsonTokenType.String && reader.GetString() is { } s)
        {
            return BigInteger.Parse(s, NumberFormatInfo.InvariantInfo);
        }

        throw new JsonException("Expected a number or a string.");
    }

    public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
    {
        if (value > long.MaxValue || value < long.MinValue)
        {
            writer.WriteStringValue(value.ToString("D", NumberFormatInfo.InvariantInfo));
        }
        else
        {
            writer.WriteNumberValue((long)value);
        }
    }
}
