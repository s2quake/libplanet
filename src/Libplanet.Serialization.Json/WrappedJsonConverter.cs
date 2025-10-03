using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json;

internal sealed class WrappedJsonConverter<T>(JsonConverter<T> innerConverter) : JsonConverter<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (ModelTypeScope.CanOmitTypeInfo(typeToConvert))
        {
            return innerConverter.Read(ref reader, typeToConvert, options);
        }

        reader.Expect(JsonTokenType.StartObject);
        reader.Read();
        var typeName = reader.ReadString("type");
        var version = reader.ReadInt32("version");
        var type = TypeUtility.GetType(typeName);
        var modelType = ModelResolver.GetType(type, version);

        reader.ReadPropertyName("value");
        if (TypeUtility.IsDefaultType(typeToConvert)
            && reader.TokenType == JsonTokenType.Number
            && reader.TryGetInt32(out var intValue)
            && intValue == 0)
        {
            reader.Read();
            reader.Expect(JsonTokenType.EndObject);
            return (T)TypeUtility.GetDefault(typeToConvert);
        }

        var obj = innerConverter.Read(ref reader, modelType, options);
        reader.Read();
        reader.Expect(JsonTokenType.EndObject);
        return obj;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            throw new JsonException("Value cannot be null.");
        }

        var type = value.GetType();
        if (ModelTypeScope.CanOmitTypeInfo(type))
        {
            innerConverter.Write(writer, value, options);
            return;
        }

        var (typeName, version) = ModelResolver.GetTypeInfo(type);
        writer.WriteStartObject();
        writer.WriteString("type", typeName);
        writer.WriteNumber("version", version);
        writer.WritePropertyName("value");

        if (TypeUtility.IsDefault(value))
        {
            writer.WriteNumberValue(0);
        }
        else
        {
            innerConverter.Write(writer, value, options);
        }

        writer.WriteEndObject();
    }
}
