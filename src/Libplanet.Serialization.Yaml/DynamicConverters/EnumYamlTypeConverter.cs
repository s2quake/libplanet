using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class EnumYamlTypeConverter : YamlTypeConverter<object>
{
    public override bool Accepts(Type type) => type.IsEnum;

    public override object? Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => Enum.Parse(type, parser.ReadStringValue());

    public override void Write(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
        => emitter.WriteStringValue($"{value}");
}
