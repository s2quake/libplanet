namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class ImmutableDictionaryJsonConverter : ImmutableCollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableDictionary<,>);

    protected override Type ImmutableStaticType => typeof(ImmutableDictionary);

    protected override bool IsDictionary => true;
}
