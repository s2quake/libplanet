using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Crypto.JsonConverters;

internal sealed class AddressJsonConverter : JsonConverter<Address>
{
    public override Address Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options
    )
    {
        if (reader.GetString() is not { } hex)
        {
            throw new JsonException("Expected a string.");
        }

        try
        {
            return Address.Parse(hex);
        }
        catch (ArgumentException e)
        {
            throw new JsonException(e.Message);
        }
    }

    public override void Write(
        Utf8JsonWriter writer, Address value, JsonSerializerOptions options)
        => writer.WriteStringValue($"{value:raw}");
}
