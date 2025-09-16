using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class ObjectJsonConverter(ModelOptions modelOptions) : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.ReadStartObject();
        var typeName = reader.ReadString("type");
        var version = reader.ReadInt32("version");
        var type = TypeUtility.GetType(typeName);
        var modelType = ModelResolver.GetType(type, version);
        var obj = TypeUtility.CreateInstance(modelType);
        var properties = ModelResolver.GetProperties(modelType);
        var propertyByName = properties.ToDictionary(p => p.Name);

        reader.ReadPropertyName("value");
        reader.Expect(JsonTokenType.StartObject);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.ReadPropertyName();
            var property = propertyByName[propertyName];
            var value = JsonSerializer.Deserialize(ref reader, property.PropertyType, options);
            property.SetValue(obj, value);
            propertyByName.Remove(propertyName);
        }

        foreach(var (_, property) in propertyByName)
        {
            var propertyType = property.PropertyType;
            if (propertyType.IsValueType)
            {
                property.SetValue(obj, TypeUtility.GetDefault(propertyType));
            }
        }

        reader.Expect(JsonTokenType.EndObject);
        reader.ReadEndObject();

        if (modelType.GetCustomAttribute<OriginModelAttribute>() is { } originModelAttribute)
        {
            var originType = originModelAttribute.Type;
            var originVersion = ModelResolver.GetVersion(originType);
            var modelVersion = ModelResolver.GetVersion(modelType);
            while (modelVersion < originVersion)
            {
                var args = new object[] { obj };
                modelType = ModelResolver.GetType(originType, modelVersion + 1);
                obj = TypeUtility.CreateInstance(modelType, args: args);
                modelVersion++;
            }
        }

        if (modelOptions.IsValidationEnabled)
        {
            ModelResolver.Validate(obj, modelOptions);
        }

        return obj;
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        var type = value.GetType();
        if (modelOptions.IsValidationEnabled && !TypeUtility.IsDefault(value, type))
        {
            ModelResolver.Validate(value, modelOptions);
        }

        if (type.GetCustomAttribute<OriginModelAttribute>() is { } originModelAttribute
            && !originModelAttribute.AllowSerialization)
        {
            throw new ModelSerializationException("LegacyModelAttribute is not supported");
        }


        var properties = ModelResolver.GetProperties(type);

        writer.WriteStartObject();
        writer.WriteString("type", ModelResolver.GetTypeName(type));
        writer.WriteNumber("version", ModelResolver.GetVersion(type));
        writer.WritePropertyName("value");
        writer.WriteStartObject();
        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            var propertyType = property.PropertyType;
            var v = property.GetValue(value);
            if (v is null)
            {
                writer.WritePropertyName(property.Name);
                writer.WriteNullValue();
            }
            else if (!propertyType.IsValueType || !TypeUtility.IsDefault(v, propertyType))
            {
                writer.WritePropertyName(property.Name);
                JsonSerializer.Serialize(writer, v, property.PropertyType, options);
            }
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
