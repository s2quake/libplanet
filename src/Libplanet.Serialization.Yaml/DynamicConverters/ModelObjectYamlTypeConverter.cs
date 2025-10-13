using System.Reflection;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class ModelObjectYamlTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
        => type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(OriginModelAttribute));

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var modelOptions = ModelOptionsScope.Current;
        var obj = TypeUtility.CreateInstance(type);
        var properties = ModelResolver.GetProperties(type);
        var propertyByName = properties.ToDictionary(p => p.Name, p => p);

        parser.ReadStartObject();
        while (parser.Current?.GetType() != typeof(MappingEnd))
        {
            var propertyName = parser.ReadPropertyName();
            var property = properties[propertyName];
            var propertyType = property.PropertyType;
            using var _ = ModelTypeScope.Push(propertyType);
            var propertyValue = rootDeserializer(property.PropertyType);
            property.SetValue(obj, propertyValue);
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

        parser.ReadEndObject();

        if (type.GetCustomAttribute<OriginModelAttribute>() is { } originModelAttribute)
        {
            var originType = originModelAttribute.Type;
            var originVersion = ModelResolver.GetVersion(originType);
            var modelVersion = ModelResolver.GetVersion(type);
            while (modelVersion < originVersion)
            {
                var args = new object[] { obj };
                type = ModelResolver.GetType(originType, modelVersion + 1);
                obj = TypeUtility.CreateInstance(type, args: args);
                modelVersion++;
            }
        }

        if (modelOptions.IsValidationEnabled)
        {
            ModelResolver.Validate(obj, modelOptions);
        }

        return obj;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(value);

        var modelOptions = ModelOptionsScope.Current;
        if (modelOptions.IsValidationEnabled && !TypeUtility.IsDefault(value))
        {
            ModelResolver.Validate(value, modelOptions);
        }

        if (type.GetCustomAttribute<OriginModelAttribute>() is { } originModelAttribute
            && !originModelAttribute.AllowSerialization)
        {
            var message = $"The type '{type}' is a legacy model and is not allowed to be serialized. " +
                          $"Because it is marked with 'OriginModelAttribute' with 'AllowSerialization = false'.";
            throw new YamlException(message);
        }

        var properties = ModelResolver.GetProperties(type);

        emitter.WriteStartObject();
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
            emitter.WritePropertyName(property.Name);
            serializer(propertyValue, propertyActualType);
        }

        emitter.WriteEndObject();
    }
}
