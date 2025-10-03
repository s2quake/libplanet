namespace Libplanet.Serialization.DynamicConverters;

internal sealed class ImmutableHashSetModelConverter : ImmutableCollectionModelConverter
{
    protected override Type ImmutableStaticType => typeof(ImmutableHashSet);

    protected override Type GenericTypeDefinition => typeof(ImmutableHashSet<>);

    protected override bool IsDictionary => false;
}
