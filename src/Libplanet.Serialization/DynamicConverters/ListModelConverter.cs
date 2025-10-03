namespace Libplanet.Serialization.DynamicConverters;

internal sealed class ListModelConverter : CollectionModelConverter
{
    protected override Type GenericTypeDefinition => typeof(List<>);

    protected override bool IsDictionary => false;
}
