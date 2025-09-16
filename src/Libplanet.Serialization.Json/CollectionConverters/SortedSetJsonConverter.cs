namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class SortedSetJsonConverter : CollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(SortedSet<>);

    protected override bool IsDictionary => false;
}
