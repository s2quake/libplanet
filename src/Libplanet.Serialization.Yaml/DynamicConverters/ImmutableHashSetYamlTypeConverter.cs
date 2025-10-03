namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class ImmutableHashSetYamlTypeConverter : ImmutableCollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableHashSet<>);

    protected override Type ImmutableStaticType => typeof(ImmutableHashSet);

    protected override bool IsDictionary => false;
}
