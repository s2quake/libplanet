using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class StringYamlTypeConverter : YamlTypeConverter<string>
{
    public override string? Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => parser.ReadStringValue();

    public override void Write(IEmitter emitter, string value, Type type, ObjectSerializer serializer)
        => emitter.WriteStringValue(value);
}
