namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class ImmutableHashSetJsonConverter : ImmutableCollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableHashSet<>);

    protected override Type ImmutableStaticType => typeof(ImmutableHashSet);

    protected override bool IsDictionary => false;
}
