namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class ImmutableSortedSetJsonConverter : ImmutableCollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableSortedSet<>);

    protected override Type ImmutableStaticType => typeof(ImmutableSortedSet);

    protected override bool IsDictionary => false;
}
