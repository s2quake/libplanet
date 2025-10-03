namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class ImmutableArrayJsonConverter : ImmutableCollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(ImmutableArray<>);

    protected override Type ImmutableStaticType => typeof(ImmutableArray);

    protected override bool IsDictionary => false;
}
