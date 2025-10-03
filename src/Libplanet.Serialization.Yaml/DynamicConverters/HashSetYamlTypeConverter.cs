namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class HashSetYamlTypeConverter : CollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(HashSet<>);

    protected override bool IsDictionary => false;
}
