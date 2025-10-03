namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class ImmutableListJsonConverter : ImmutableCollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableList<>);

    protected override Type ImmutableStaticType => typeof(ImmutableList);

    protected override bool IsDictionary => false;
}
