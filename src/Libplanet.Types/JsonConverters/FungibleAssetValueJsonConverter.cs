using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Types.Assets;

namespace Libplanet.Types.JsonConverters;

internal class FungibleAssetValueJsonConverter : JsonConverter<FungibleAssetValue>
{
    public override FungibleAssetValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(
                $"Expected an object representation of {nameof(FungibleAssetValue)}."
            );
        }

        reader.Read();

        string quantityFieldName =
            options.PropertyNamingPolicy?.ConvertName("Quantity") ?? "Quantity";
        string currencyFieldName =
            options.PropertyNamingPolicy?.ConvertName("Currency") ?? "Currency";
        string? quantityString = null;
        Currency? currency = null;

        while (reader.TokenType != JsonTokenType.EndObject &&
               (quantityString is null || currency is null))
        {
            if (quantityString is { } && currency is { })
            {
                throw new JsonException($"Unexpected token: {reader.TokenType}.");
            }

            if (!(reader.GetString() is { } propName))
            {
                throw new JsonException("Expected a field name.");
            }

            reader.Read();
            switch (propName.ToLowerInvariant())
            {
                case "quantity":
                    if (options.PropertyNameCaseInsensitive || propName == quantityFieldName)
                    {
                        quantityString = reader.GetString();
                        reader.Read();
                        if (quantityString is null)
                        {
                            throw new JsonException("Expected a string value.");
                        }
                    }

                    break;

                case "currency":
                    if (options.PropertyNameCaseInsensitive || propName == currencyFieldName)
                    {
                        currency = JsonSerializer.Deserialize<Currency>(ref reader, options);
                        if (currency is null)
                        {
                            throw new JsonException(
                                $"Expected an object representation of {nameof(Currency)}.");
                        }
                    }

                    break;

                default:
                    throw new JsonException($"Unexpected field name: {propName}.");
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException($"Unexpected token: {reader.TokenType}.");
        }

        reader.Read();

        if (!(quantityString is { } q))
        {
            throw new JsonException($"Missing field: \"{quantityFieldName}\".");
        }

        if (!(currency is { } c))
        {
            throw new JsonException($"Missing field: \"{currencyFieldName}\".");
        }

        return FungibleAssetValue.Parse(c, q);
    }

    public override void Write(
        Utf8JsonWriter writer,
        FungibleAssetValue value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();
        writer.WriteString(
            options.PropertyNamingPolicy?.ConvertName("Quantity") ?? "Quantity",
            value.GetQuantityString()
        );
        writer.WritePropertyName(
            options.PropertyNamingPolicy?.ConvertName("Currency") ?? "Currency");
        JsonSerializer.Serialize(writer, value.Currency, options);
        writer.WriteEndObject();
    }
}
