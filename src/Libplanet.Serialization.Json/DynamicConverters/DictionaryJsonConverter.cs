namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class DictionaryJsonConverter : CollectionJsonConverter
{
    protected override Type GenericTypeDefinition => typeof(Dictionary<,>);

    protected override bool IsDictionary => true;
}
