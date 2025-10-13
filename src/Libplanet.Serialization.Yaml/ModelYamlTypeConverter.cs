using YamlDotNet.Serialization;
using YamlDotNet.Core;

namespace Libplanet.Serialization.Yaml;

public sealed class ModelYamlTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => true;

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.Current is YamlDotNet.Core.Events.Scalar { Style: ScalarStyle.Plain, Value: "null" })
        {
            parser.MoveNext();
            return null;
        }

        if (ModelTypeScope.CanOmitTypeInfo(type))
        {
            if (type == typeof(object))
            {
                throw new NotSupportedException("Cannot deserialize to 'object' type without type info.");
            }

            if (!ModelYamlResolver.TryGetConverter(type, out var converter))
            {
                throw new YamlException($"No converter found for type {type}.");
            }

            return converter.ReadYaml(parser, type, rootDeserializer);
        }
        else
        {
            object? value = null;
            parser.ReadStartObject();
            var typeName = parser.ReadString("type");
            var version = parser.ReadInt32("version");
            var actualType = TypeUtility.GetType(typeName);
            var modelType = ModelResolver.GetType(actualType, version);

            if (!ModelYamlResolver.TryGetConverter(modelType, out var converter))
            {
                throw new YamlException($"No converter found for type {modelType}.");
            }

            parser.ReadPropertyName("value");
            if (TypeUtility.HasDefaultValue(modelType)
                && parser.Current is YamlDotNet.Core.Events.Scalar { Style: ScalarStyle.Plain, Value: "0" })
            {
                parser.MoveNext();
                parser.ReadEndObject();
                return TypeUtility.GetDefault(modelType);
            }

            value = converter.ReadYaml(parser, modelType, rootDeserializer);
            parser.ReadEndObject();
            return value;
        }
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is null)
        {
            emitter.WriteNullValue();
            return;
        }

        if (type == typeof(object))
        {
            throw new NotSupportedException("Cannot serialize to 'object' type.");
        }

        if (!ModelYamlResolver.TryGetConverter(type, out var converter))
        {
            var message = $"Type '{type}' is not supported or not registered in known types.";
            throw new InvalidModelException(message, type);
        }

        if (ModelTypeScope.CanOmitTypeInfo(type))
        {
            converter.WriteYaml(emitter, value, type, serializer);
            return;
        }

        var modelOptions = ModelOptionsScope.Current;
        var (typeName, version) = ModelResolver.GetTypeInfo(type);
        emitter.WriteStartObject();
        emitter.WriteString("type", typeName);
        emitter.WriteNumber("version", version);
        emitter.WritePropertyName("value");

        if (TypeUtility.IsDefault(value) && !modelOptions.EmitDefaultValues)
        {
            emitter.WriteNumberValue(0);
        }
        else
        {
            converter.WriteYaml(emitter, value, type, serializer);
        }

        emitter.WriteEndObject();
    }
}
