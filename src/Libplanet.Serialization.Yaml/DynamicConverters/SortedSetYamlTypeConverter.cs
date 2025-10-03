namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class SortedSetYamlTypeConverter : CollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(SortedSet<>);

    protected override bool IsDictionary => false;
}
