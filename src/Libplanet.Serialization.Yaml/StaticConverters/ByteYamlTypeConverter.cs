using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class ByteYamlTypeConverter : YamlTypeConverter<byte>
{
    public override byte Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => parser.ReadByteValue();

    public override void Write(IEmitter emitter, byte value, Type type, ObjectSerializer serializer)
        => emitter.WriteNumberValue(value);
}
