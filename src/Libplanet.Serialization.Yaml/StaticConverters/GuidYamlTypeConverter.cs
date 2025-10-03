using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class GuidYamlTypeConverter : YamlTypeConverter<Guid>
{
    public override Guid Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => Guid.Parse(parser.ReadStringValue());

    public override void Write(IEmitter emitter, Guid value, Type type, ObjectSerializer serializer)
        => emitter.WriteStringValue(value.ToString("D", CultureInfo.InvariantCulture));
}
