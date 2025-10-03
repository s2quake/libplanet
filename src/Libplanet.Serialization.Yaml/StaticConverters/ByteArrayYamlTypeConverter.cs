using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class ByteArrayYamlTypeConverter : YamlTypeConverter<byte[]>
{
    public override byte[] Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => Convert.FromHexString(parser.ReadStringValue());

    public override void Write(IEmitter emitter, byte[] value, Type type, ObjectSerializer serializer)
        => emitter.WriteStringValue(Convert.ToHexString(value).ToLowerInvariant());
}
