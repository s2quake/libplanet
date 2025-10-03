namespace Libplanet.Serialization.DynamicConverters;

internal sealed class ImmutableDictionaryModelConverter : ImmutableCollectionModelConverter
{
    protected override Type ImmutableStaticType => typeof(ImmutableDictionary);

    protected override Type GenericTypeDefinition => typeof(ImmutableDictionary<,>);

    protected override bool IsDictionary => true;
}
