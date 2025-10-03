namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class ImmutableListYamlTypeConverter : ImmutableCollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableList<>);

    protected override Type ImmutableStaticType => typeof(ImmutableList);

    protected override bool IsDictionary => false;
}
