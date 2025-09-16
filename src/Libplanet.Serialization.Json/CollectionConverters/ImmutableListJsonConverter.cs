namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class ImmutableListJsonConverter : ImmutableCollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableList<>);

    protected override Type ImmutableStaticType => typeof(ImmutableList);

    protected override bool IsDictionary => false;
}
