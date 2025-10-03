namespace Libplanet.Serialization.DynamicConverters;

internal sealed class ImmutableSortedSetModelConverter : ImmutableCollectionModelConverter
{
    protected override Type ImmutableStaticType => typeof(ImmutableSortedSet);

    protected override Type GenericTypeDefinition => typeof(ImmutableSortedSet<>);

    protected override bool IsDictionary => false;
}
