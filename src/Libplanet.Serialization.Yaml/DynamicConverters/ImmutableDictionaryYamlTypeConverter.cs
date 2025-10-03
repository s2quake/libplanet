namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class ImmutableDictionaryYamlTypeConverter : ImmutableCollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableDictionary<,>);

    protected override Type ImmutableStaticType => typeof(ImmutableDictionary);

    protected override bool IsDictionary => true;
}
