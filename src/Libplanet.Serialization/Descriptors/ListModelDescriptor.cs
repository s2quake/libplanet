namespace Libplanet.Serialization.Descriptors;

internal sealed class ListModelDescriptor : CollectionModelDescriptor
{
    protected override Type GenericTypeDefinition => typeof(List<>);

    protected override bool IsDictionary => false;
}
