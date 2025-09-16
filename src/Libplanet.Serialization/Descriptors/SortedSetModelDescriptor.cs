namespace Libplanet.Serialization.Descriptors;

internal sealed class SortedSetModelDescriptor : CollectionModelDescriptor
{
    protected override Type GenericTypeDefinition => typeof(SortedSet<>);

    protected override bool IsDictionary => false;
}
