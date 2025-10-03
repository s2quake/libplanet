using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class TimeSpanYamlTypeConverter : YamlTypeConverter<TimeSpan>
{
    public override TimeSpan Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => TimeSpan.FromTicks(parser.ReadInt64Value());

    public override void Write(IEmitter emitter, TimeSpan value, Type type, ObjectSerializer serializer)
        => emitter.WriteNumberValue(value.Ticks);
}
