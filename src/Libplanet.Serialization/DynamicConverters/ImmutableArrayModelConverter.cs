namespace Libplanet.Serialization.DynamicConverters;

internal sealed class ImmutableArrayModelConverter : ImmutableCollectionModelConverter
{
    protected override Type ImmutableStaticType => typeof(ImmutableArray);

    protected override Type GenericTypeDefinition => typeof(ImmutableArray<>);

    protected override bool IsDictionary => false;
}
