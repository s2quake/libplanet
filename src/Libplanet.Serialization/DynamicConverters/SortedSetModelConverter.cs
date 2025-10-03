namespace Libplanet.Serialization.DynamicConverters;

internal sealed class SortedSetModelConverter : CollectionModelConverter
{
    protected override Type GenericTypeDefinition => typeof(SortedSet<>);

    protected override bool IsDictionary => false;
}
