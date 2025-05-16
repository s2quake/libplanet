namespace Libplanet.Serialization.Converters;

internal sealed class BigIntegerTypeConverter : InternalTypeConverterBase<BigInteger>
{
    protected override BigInteger ConvertFromValue(byte[] value) => new(value);

    protected override byte[] ConvertToValue(BigInteger value) => value.ToByteArray();
}
