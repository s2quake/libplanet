using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Libplanet.Serialization.Json.Descriptors;

internal sealed class ObjectModelJsonDescriptor : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        var type = value.GetType();
        writer.WriteStartObject();
        writer.WriteString("type", ModelResolver.GetTypeName(type));
        writer.WriteNumber("version", ModelResolver.GetVersion(type));
        writer.WritePropertyName("value");

        var properties = ModelResolver.GetProperties(type);

        writer.WriteStartObject();
        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            var v = property.GetValue(value);
            writer.WritePropertyName(property.Name);
            JsonSerializer.Serialize(writer, v, property.PropertyType, options);
        }

        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}
