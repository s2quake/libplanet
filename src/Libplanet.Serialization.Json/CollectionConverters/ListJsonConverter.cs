namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class ListJsonConverter : CollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(List<>);

    protected override bool IsDictionary => false;
}
