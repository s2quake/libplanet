using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json;

public sealed class ModelJsonConverterFactory : JsonConverterFactory
{
    private readonly UnknownJsonConverter _unknownJsonConverter = new();

    public override bool CanConvert(Type typeToConvert) => true;

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(object))
        {
            return _unknownJsonConverter;
        }

        if (ModelJsonResolver.TryGetConverter(typeToConvert, out var converter))
        {
            return converter;
        }

        throw new InvalidModelException(
            $"Type '{typeToConvert}' is not supported or not registered in known types.", typeToConvert);
    }
}
