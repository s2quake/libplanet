using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class ModelObjectJsonConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsDefined(typeof(ModelAttribute)) || typeToConvert.IsDefined(typeof(OriginModelAttribute));

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var modelOptions = ModelOptionsScope.Current;
        var modelType = typeToConvert;

        var obj = TypeUtility.CreateInstance(modelType);
        var properties = ModelResolver.GetProperties(modelType);
        var propertyByName = properties.ToDictionary(p => p.Name, p => p);

        reader.Expect(JsonTokenType.StartObject);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.ReadPropertyName();
            var property = properties[propertyName];
            var propertyType = property.PropertyType;
            using var _ = ModelTypeScope.Push(propertyType);
            var value = JsonSerializer.Deserialize(ref reader, propertyType, options);
            property.SetValue(obj, value);
            propertyByName.Remove(propertyName);
        }

        foreach (var (_, property) in propertyByName)
        {
            var propertyType = property.PropertyType;
            if (TypeUtility.TryGetDefault(propertyType, out var defaultValue))
            {
                property.SetValue(obj, defaultValue);
            }
        }

        reader.Expect(JsonTokenType.EndObject);

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
        var modelOptions = ModelOptionsScope.Current;
        var type = value.GetType();
        if (modelOptions.IsValidationEnabled && !TypeUtility.IsDefault(value))
        {
            ModelResolver.Validate(value, modelOptions);
        }

        if (type.GetCustomAttribute<OriginModelAttribute>() is { } originModelAttribute
            && !originModelAttribute.AllowSerialization)
        {
            var message = $"The type '{type}' is a legacy model and is not allowed to be serialized. " +
                          $"Because it is marked with 'OriginModelAttribute' with 'AllowSerialization = false'.";
            throw new JsonException(message);
        }

        var properties = ModelResolver.GetProperties(type);
        writer.WriteStartObject();
        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            var propertyType = property.PropertyType;
            var propertyValue = property.GetValue(value);
            var isDefault = TypeUtility.IsDefault(propertyValue);
            var emitDefaultValue = modelOptions.EmitDefaultValues || property.EmitDefaultValue;
            if (isDefault && !emitDefaultValue)
            {
                continue;
            }

            var propertyActualType = TypeUtility.GetActualType(propertyValue, propertyType);
            using var _ = ModelTypeScope.Push(propertyType);
            writer.WritePropertyName(property.Name);
            JsonSerializer.Serialize(writer, propertyValue, propertyActualType, options);
        }

        writer.WriteEndObject();
    }
}
