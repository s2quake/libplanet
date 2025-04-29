using System.Text.Json;
using System.Text.Json.Serialization;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Types.JsonConverters;

internal sealed class CurrencyJsonConverter : JsonConverter<Currency>
{
    public override Currency Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected an object representation of {nameof(Currency)}.");
        }

        reader.Read();

        var ticker = string.Empty;
        var decimalPlaces = (byte)0;
        var maximumSupply = BigInteger.Zero;
        var minters = ImmutableArray<Address>.Empty;
        var hash = string.Empty;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            if (!(reader.GetString() is { } propertyName))
            {
                throw new JsonException("Expected a field name.");
            }

            reader.Read();
            switch (propertyName)
            {
                case "ticker":
                    ticker = reader.GetString()
                        ?? throw new JsonException("Ticker cannot be null.");
                    break;
                case "decimalPlaces":
                    decimalPlaces = reader.GetByte();
                    break;
                case "maximumSupply":
                    maximumSupply = BigInteger.Parse(reader.GetString()!);
                    break;
                case "minters":
                    minters = ReadMinters(ref reader);
                    break;
                case "hash":
                    hash = reader.GetString() ?? throw new JsonException("Hash cannot be null.");
                    break;
                default:
                    throw new JsonException($"Unexpected property: {propertyName}.");
            }

            reader.Read();
        }

        var currency = new Currency
        {
            Ticker = ticker,
            DecimalPlaces = decimalPlaces,
            MaximumSupply = maximumSupply,
            Minters = [.. minters],
        };

        if (currency.Hash.ToString() != hash)
        {
            throw new JsonException($"Hash mismatch: {currency.Hash} != {hash}");
        }

        return currency;
    }

    public override void Write(
        Utf8JsonWriter writer, Currency value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("hash", value.Hash.ToString());
        writer.WriteString("ticker", value.Ticker);
        writer.WriteNumber("decimalPlaces", value.DecimalPlaces);
        writer.WriteString("maximumSupply", $"{value.MaximumSupply}");
        writer.WriteStartArray("minters");
        foreach (var minter in value.Minters)
        {
            writer.WriteStringValue(minter.ToString());
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static ImmutableArray<Address> ReadMinters(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected an array of minters.");
        }

        var minters = ImmutableArray.CreateBuilder<Address>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected a string representation of an address.");
            }

            var address = Address.Parse(reader.GetString()!);
            minters.Add(address);
        }

        return minters.ToImmutable();
    }
}
