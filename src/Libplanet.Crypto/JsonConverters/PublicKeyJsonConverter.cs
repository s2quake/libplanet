using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Crypto.JsonConverters;

internal sealed class PublicKeyJsonConverter : JsonConverter<PublicKey>
{
    public override PublicKey Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.GetString() is not { } hex)
        {
            throw new JsonException("Expected a string.");
        }

        try
        {
            return PublicKey.Parse(hex!);
        }
        catch (Exception e) when (e is ArgumentException || e is FormatException)
        {
            throw new JsonException(e.Message);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        PublicKey value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("c", null));
}
