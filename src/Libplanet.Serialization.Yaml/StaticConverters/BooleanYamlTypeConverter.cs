using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class BooleanYamlTypeConverter : YamlTypeConverter<bool>
{
    public override bool Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => parser.ReadBooleanValue();

    public override void Write(IEmitter emitter, bool value, Type type, ObjectSerializer serializer)
        => emitter.WriteBooleanValue(value);
}
