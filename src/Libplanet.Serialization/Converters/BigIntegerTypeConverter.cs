namespace Libplanet.Serialization.Converters;

internal sealed class BigIntegerTypeConverter : TypeConverterBase<BigInteger, Bencodex.Types.Integer>
{
    protected override BigInteger ConvertFromValue(Bencodex.Types.Integer value) => value.Value;

    protected override Bencodex.Types.Integer ConvertToValue(BigInteger value) => new(value);
}
