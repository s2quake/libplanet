namespace Libplanet.Serialization.DynamicConverters;

internal sealed class HashSetModelConverter : CollectionModelConverter
{
    protected override Type GenericTypeDefinition => typeof(HashSet<>);

    protected override bool IsDictionary => false;
}
