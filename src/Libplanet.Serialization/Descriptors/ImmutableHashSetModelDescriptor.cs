namespace Libplanet.Serialization.Descriptors;

internal sealed class ImmutableHashSetModelDescriptor : ImmutableCollectionModelDescriptor
{
    protected override Type ImmutableStaticType => typeof(ImmutableHashSet);

    protected override Type GenericTypeDefinition => typeof(ImmutableHashSet<>);

    protected override bool IsDictionary => false;
}
