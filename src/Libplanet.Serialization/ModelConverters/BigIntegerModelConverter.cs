namespace Libplanet.Serialization.ModelConverters;

internal sealed class BigIntegerTypeConverter : InternalModelConverterBase<BigInteger>
{
    protected override BigInteger ConvertFromValue(byte[] value) => new(value);

    protected override byte[] ConvertToValue(BigInteger value) => value.ToByteArray();
}
