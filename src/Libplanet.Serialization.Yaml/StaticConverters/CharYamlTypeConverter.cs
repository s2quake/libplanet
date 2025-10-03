using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml.StaticConverters;

internal sealed class CharYamlTypeConverter : YamlTypeConverter<char>
{
    public override char Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => parser.ReadStringValue() is string s && s.Length is 1
            ? s[0]
            : throw new YamlException("Expected a single character string.");

    public override void Write(IEmitter emitter, char value, Type type, ObjectSerializer serializer)
    {
        if (char.IsSurrogate(value))
        {
            throw new YamlException("Cannot serialize a surrogate character.");
        }

        emitter.WriteStringValue(value.ToString());
    }
}
