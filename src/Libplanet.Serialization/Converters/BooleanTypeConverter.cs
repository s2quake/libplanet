namespace Libplanet.Serialization.Converters;

internal sealed class BooleanTypeConverter : TypeConverterBase<bool, Bencodex.Types.Boolean>
{
    protected override bool ConvertFromValue(Bencodex.Types.Boolean value) => value.Value;

    protected override Bencodex.Types.Boolean ConvertToValue(bool value) => new(value);
}
