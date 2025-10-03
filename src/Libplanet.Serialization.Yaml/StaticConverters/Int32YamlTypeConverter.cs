using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class Int32YamlTypeConverter : YamlTypeConverter<int>
{
    public override int Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => parser.ReadInt32Value();

    public override void Write(IEmitter emitter, int value, Type type, ObjectSerializer serializer)
        => emitter.WriteNumberValue(value);
}
