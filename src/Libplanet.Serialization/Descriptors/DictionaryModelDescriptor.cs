namespace Libplanet.Serialization.Descriptors;

internal sealed class DictionaryModelDescriptor : CollectionModelDescriptor
{
    protected override Type GenericTypeDefinition => typeof(Dictionary<,>);

    protected override bool IsDictionary => true;
}
