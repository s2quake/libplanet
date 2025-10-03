namespace Libplanet.Serialization.DynamicConverters;

internal sealed class ImmutableListModelConverter : ImmutableCollectionModelConverter
{
    protected override Type ImmutableStaticType => typeof(ImmutableList);

    protected override Type GenericTypeDefinition => typeof(ImmutableList<>);

    protected override bool IsDictionary => false;
}
