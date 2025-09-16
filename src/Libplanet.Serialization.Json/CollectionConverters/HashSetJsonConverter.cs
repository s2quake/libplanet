namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class HashSetJsonConverter : CollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(HashSet<>);

    protected override bool IsDictionary => false;
}
