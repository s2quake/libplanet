namespace Libplanet.Serialization.DynamicConverters;

internal sealed class ImmutableSortedDictionaryModelConverter : ImmutableCollectionModelConverter
{
    protected override Type ImmutableStaticType => typeof(ImmutableSortedDictionary);

    protected override Type GenericTypeDefinition => typeof(ImmutableSortedDictionary<,>);

    protected override bool IsDictionary => true;
}
