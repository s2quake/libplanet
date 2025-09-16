namespace Libplanet.Serialization.Descriptors;

internal sealed class ImmutableSortedDictionaryModelDescriptor : ImmutableCollectionModelDescriptor
{
    protected override Type ImmutableStaticType => typeof(ImmutableSortedDictionary);

    protected override Type GenericTypeDefinition => typeof(ImmutableSortedDictionary<,>);

    protected override bool IsDictionary => true;
}
