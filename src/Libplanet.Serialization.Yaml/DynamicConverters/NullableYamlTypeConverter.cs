using System.Diagnostics;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class NullableYamlTypeConverter : YamlTypeConverter<object>
{
    public override bool Accepts(Type type) => Nullable.GetUnderlyingType(type) is not null;

    public override object? Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (Nullable.GetUnderlyingType(ModelTypeScope.Current) is not { } underlyingType)
        {
            return rootDeserializer(type);
        }
        else if (Nullable.GetUnderlyingType(type) == underlyingType)
        {
            using var _ = ModelTypeScope.Push(underlyingType);
            return rootDeserializer(underlyingType);
        }
        else
        {
            throw new YamlException(
                $"Type '{type}' is not a nullable type of '{underlyingType}'.");
        }
    }

    public override void Write(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
    {
        if (Nullable.GetUnderlyingType(ModelTypeScope.Current) is not { } underlyingType)
        {
            serializer(value, value.GetType());
        }
        else if (value.GetType() == underlyingType)
        {
            using var _ = ModelTypeScope.Push(underlyingType);
            serializer(value, underlyingType);
        }
        else
        {
            throw new YamlException(
                $"Value type '{value.GetType()}' does not match the nullable type '{underlyingType}'.");
        }
    }
}
