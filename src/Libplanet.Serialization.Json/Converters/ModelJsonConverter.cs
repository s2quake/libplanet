using System.Text.Json;
using System.Text.Json.Serialization;
using Libplanet.Serialization.Json.Descriptors;

namespace Libplanet.Serialization.Json.Converters;

public sealed class ModelJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (typeToConvert.IsDefined(typeof(ModelAttribute), inherit: false))
        {
            return true;
        }

        if (typeToConvert.IsDefined(typeof(ModelConverterAttribute), inherit: false))
        {
            return true;
        }

        if (ModelJsonResolver.TryGetConverter(typeToConvert, out _))
        {
            return true;
        }

        if (ModelJsonResolver.TryGetDescriptor(typeToConvert, out _))
        {
            return true;
        }

        return false;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert.IsDefined(typeof(ModelAttribute), inherit: false))
        {
            return new ObjectModelJsonDescriptor();
        }

        if (ModelJsonResolver.TryGetConverter(typeToConvert, out var c0))
        {
            return c0;
        }

        if (ModelJsonResolver.TryGetDescriptor(typeToConvert, out var c1))
        {
            return c1;
        }

        return null;
    }
}
