using System.Text.Json;

namespace Libplanet.Serialization.Json;

public static class ModelJsonSerializer
{
    public static string Serialize(object? obj) => Serialize(obj, ModelOptions.Empty);

    public static string Serialize(object? obj, ModelOptions options)
    {
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new ModelJsonConverterFactory(options),
            },
        });
    }

    public static object Deserialize(string json)
        => Deserialize(json, ModelOptions.Empty);

    public static object Deserialize(string json, ModelOptions options)
    {
        var obj = JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions
        {
            Converters =
            {
                new ModelJsonConverterFactory(options),
            },
        });

        return obj ?? throw new ModelSerializationException("Failed to deserialize from string.");
    }

    public static T Deserialize<T>(string json)
        where T : notnull
        => Deserialize<T>(json, ModelOptions.Empty);

    public static T Deserialize<T>(string json, ModelOptions options)
        where T : notnull
    {
        var obj = JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions
        {
            Converters =
            {
                new ModelJsonConverterFactory(options),
            },
        });

        if (obj is not T t)
        {
            throw new ModelSerializationException("Failed to deserialize from string.");
        }

        return t;
    }

    public static T Clone<T>(T obj) where T : notnull => Clone(obj, ModelOptions.Empty);

    public static T Clone<T>(T obj, ModelOptions options)
        where T : notnull
    {
        var serialized = Serialize(obj, options);
        return Deserialize<T>(serialized, options);
    }
}
