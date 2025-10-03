namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class ImmutableSortedDictionaryYamlTypeConverter : ImmutableCollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableSortedDictionary<,>);

    protected override Type ImmutableStaticType => typeof(ImmutableSortedDictionary);

    protected override bool IsDictionary => true;
}
