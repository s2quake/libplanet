namespace Libplanet.Serialization.Descriptors;

internal sealed class ImmutableSortedSetModelDescriptor : ImmutableCollectionModelDescriptor
{
    protected override Type ImmutableStaticType => typeof(ImmutableSortedSet);

    protected override Type GenericTypeDefinition => typeof(ImmutableSortedSet<>);

    protected override bool IsDictionary => false;
}
