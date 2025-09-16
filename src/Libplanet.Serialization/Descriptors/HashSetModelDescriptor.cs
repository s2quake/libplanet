namespace Libplanet.Serialization.Descriptors;

internal sealed class HashSetModelDescriptor : CollectionModelDescriptor
{
    protected override Type GenericTypeDefinition => typeof(HashSet<>);

    protected override bool IsDictionary => false;
}
