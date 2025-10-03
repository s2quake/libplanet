namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class ImmutableSortedSetYamlTypeConverter : ImmutableCollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableSortedSet<>);

    protected override Type ImmutableStaticType => typeof(ImmutableSortedSet);

    protected override bool IsDictionary => false;
}
