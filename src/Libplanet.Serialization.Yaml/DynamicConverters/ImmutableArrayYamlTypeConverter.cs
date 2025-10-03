namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class ImmutableArrayYamlTypeConverter : ImmutableCollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableArray<>);

    protected override Type ImmutableStaticType => typeof(ImmutableArray);

    protected override bool IsDictionary => false;
}
