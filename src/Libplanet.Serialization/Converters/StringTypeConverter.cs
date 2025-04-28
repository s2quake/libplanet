namespace Libplanet.Serialization.Converters;

internal sealed class StringTypeConverter : TypeConverterBase<string, Bencodex.Types.Text>
{
    protected override string ConvertFromValue(Bencodex.Types.Text value) => value.Value;

    protected override Bencodex.Types.Text ConvertToValue(string value) => new(value);
}
