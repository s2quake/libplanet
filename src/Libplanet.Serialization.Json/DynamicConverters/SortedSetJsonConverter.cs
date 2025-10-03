namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class SortedSetJsonConverter : CollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(SortedSet<>);

    protected override bool IsDictionary => false;
}
