namespace Libplanet.Serialization.Descriptors;

internal sealed class ImmutableDictionaryModelDescriptor : ImmutableCollectionModelDescriptor
{
    protected override Type ImmutableStaticType => typeof(ImmutableDictionary);

    protected override Type GenericTypeDefinition => typeof(ImmutableDictionary<,>);

    protected override bool IsDictionary => true;
}
