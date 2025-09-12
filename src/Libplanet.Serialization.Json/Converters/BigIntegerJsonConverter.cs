using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.Converters;

public sealed class BigIntegerJsonConverter : JsonConverter<BigInteger>
{
    public override BigInteger Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number)
        {
            throw new JsonException($"Expected a number, not a {reader.TokenType}");
        }

        var bytes = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
        var digits = Encoding.ASCII.GetString(bytes);
        return BigInteger.Parse(digits, NumberFormatInfo.InvariantInfo);
    }

    public override void Write(
        Utf8JsonWriter writer,
        BigInteger value,
        JsonSerializerOptions options) =>
        writer.WriteRawValue(value.ToString(NumberFormatInfo.InvariantInfo), false);
}
