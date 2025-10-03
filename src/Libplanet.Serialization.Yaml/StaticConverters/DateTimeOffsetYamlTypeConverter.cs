using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class DateTimeOffsetYamlTypeConverter : YamlTypeConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => DateTimeOffset.Parse(parser.ReadStringValue(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public override void Write(IEmitter emitter, DateTimeOffset value, Type type, ObjectSerializer serializer)
        => emitter.WriteStringValue(value.ToString("o", CultureInfo.InvariantCulture));
}
