namespace Libplanet.Serialization.DynamicConverters;

internal sealed class DictionaryModelConverter : CollectionModelConverter
{
    protected override Type GenericTypeDefinition => typeof(Dictionary<,>);

    protected override bool IsDictionary => true;
}
