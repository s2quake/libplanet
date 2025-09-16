namespace Libplanet.Serialization.Descriptors;

internal sealed class ImmutableArrayModelDescriptor : ImmutableCollectionModelDescriptor
{
    protected override Type ImmutableStaticType => typeof(ImmutableArray);

    protected override Type GenericTypeDefinition => typeof(ImmutableArray<>);

    protected override bool IsDictionary => false;
}
