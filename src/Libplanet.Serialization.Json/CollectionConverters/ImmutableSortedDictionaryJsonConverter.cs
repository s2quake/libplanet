namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class ImmutableSortedDictionaryJsonConverter : ImmutableCollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableSortedDictionary<,>);

    protected override Type ImmutableStaticType => typeof(ImmutableSortedDictionary);

    protected override bool IsDictionary => true;
}
