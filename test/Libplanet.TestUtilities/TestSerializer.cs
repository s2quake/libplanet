using Libplanet.Serialization;
using Libplanet.Serialization.Json;
using Libplanet.Serialization.Yaml;

namespace Libplanet.TestUtilities;

public static class TestSerializer
{
    public static object Serialize<T>(T obj, string format)
        where T : notnull
    {
        return format.ToLowerInvariant() switch
        {
            "binary" => ModelSerializer.Serialize(obj),
            "json" => ModelJsonSerializer.Serialize(obj),
            "yaml" => ModelYamlSerializer.Serialize(obj),
            _ => throw new NotSupportedException($"The format '{format}' is not supported."),
        };
    }

    public static T Deserialize<T>(object data, string format)
        where T : notnull
    {
        return format.ToLowerInvariant() switch
        {
            "binary" => ModelSerializer.Deserialize<T>((byte[])data),
            "json" => ModelJsonSerializer.Deserialize<T>((string)data),
            "yaml" => ModelYamlSerializer.Deserialize<T>((string)data),
            _ => throw new NotSupportedException($"The format '{format}' is not supported."),
        };
    }

    public static T Clone<T>(T obj, string format)
        where T : notnull
        => Deserialize<T>(Serialize(obj, format), format);
}
