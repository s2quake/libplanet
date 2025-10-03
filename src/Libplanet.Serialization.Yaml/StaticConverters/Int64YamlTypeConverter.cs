using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class Int64YamlTypeConverter : YamlTypeConverter<long>
{
    public override long Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => parser.ReadInt64Value();

    public override void Write(IEmitter emitter, long value, Type type, ObjectSerializer serializer)
        => emitter.WriteNumberValue(value);
}
