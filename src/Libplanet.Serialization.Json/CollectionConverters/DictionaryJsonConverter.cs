namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class DictionaryJsonConverter : CollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(Dictionary<,>);

    protected override bool IsDictionary => true;
}
