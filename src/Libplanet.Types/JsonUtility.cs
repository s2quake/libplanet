using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Types;

public static class JsonUtility
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions SchemaSerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { ExcludeEmptyStrings },
        },
    };

    public static string Serialize(object value) => Serialize(value);

    public static Task<string> SerializeAsync(object value, CancellationToken cancellationToken)
        => Task.Run(() => Serialize(value), cancellationToken);

    public static string SerializeSchema(object value)
        => JsonSerializer.Serialize(value, SchemaSerializerOptions);

    public static T Deserialize<T>(string value)
    {
        if (JsonSerializer.Deserialize<T>(value, SerializerOptions) is T t)
        {
            return t;
        }

        throw new ArgumentException("Cannot deserialize the object.", nameof(value));
    }

    public static T DeserializeSchema<T>(string value)
    {
        if (JsonSerializer.Deserialize<T>(value, SchemaSerializerOptions) is T t)
        {
            return t;
        }

        throw new ArgumentException("Cannot deserialize the object.", nameof(value));
    }

    private static void ExcludeEmptyStrings(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object)
        {
            foreach (var property in jsonTypeInfo.Properties)
            {
                if (property.PropertyType == typeof(string))
                {
                    property.ShouldSerialize = ShouldSerialize;
                }
            }
        }

        static bool ShouldSerialize(object obj, object? value)
            => value is string @string && string.IsNullOrEmpty(@string) is false;
    }
}
