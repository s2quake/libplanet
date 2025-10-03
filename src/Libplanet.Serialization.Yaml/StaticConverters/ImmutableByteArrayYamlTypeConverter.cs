using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class ImmutableByteArrayYamlTypeConverter : YamlTypeConverter<ImmutableArray<byte>>
{
    public override ImmutableArray<byte> Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => ImmutableArray.Create(Convert.FromHexString(parser.ReadStringValue()));

    public override void Write(IEmitter emitter, ImmutableArray<byte> value, Type type, ObjectSerializer serializer)
        => emitter.WriteStringValue(Convert.ToHexString([.. value]).ToLowerInvariant());
}
