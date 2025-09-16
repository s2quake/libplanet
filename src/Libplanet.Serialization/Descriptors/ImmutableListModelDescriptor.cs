namespace Libplanet.Serialization.Descriptors;

internal sealed class ImmutableListModelDescriptor : ImmutableCollectionModelDescriptor
{
    protected override Type ImmutableStaticType => typeof(ImmutableList);

    protected override Type GenericTypeDefinition => typeof(ImmutableList<>);

    protected override bool IsDictionary => false;
}
