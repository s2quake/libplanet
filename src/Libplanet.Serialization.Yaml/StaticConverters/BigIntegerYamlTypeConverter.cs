using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class BigIntegerYamlTypeConverter : YamlTypeConverter<BigInteger>
{
    public override BigInteger Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => BigInteger.Parse(parser.ReadStringValue(), NumberFormatInfo.InvariantInfo);

    public override void Write(IEmitter emitter, BigInteger value, Type type, ObjectSerializer serializer)
    {
        if (value > long.MaxValue || value < long.MinValue)
        {
            emitter.WriteStringValue(value.ToString("D", NumberFormatInfo.InvariantInfo));
        }
        else
        {
            emitter.WriteNumberValue((long)value);
        }
    }
}
