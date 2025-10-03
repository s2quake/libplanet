namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class ListYamlTypeConverter : CollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(List<>);

    protected override bool IsDictionary => false;
}
