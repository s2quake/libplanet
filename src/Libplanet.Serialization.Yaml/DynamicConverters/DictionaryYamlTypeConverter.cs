namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class DictionaryYamlTypeConverter : CollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(Dictionary<,>);

    protected override bool IsDictionary => true;
}
