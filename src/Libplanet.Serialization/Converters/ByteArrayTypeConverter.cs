namespace Libplanet.Serialization.Converters;

internal sealed class ByteArrayTypeConverter : TypeConverterBase<byte[], Bencodex.Types.Binary>
{
    protected override byte[] ConvertFromValue(Bencodex.Types.Binary value) => [.. value];

    protected override Bencodex.Types.Binary ConvertToValue(byte[] value) => new(value);
}
