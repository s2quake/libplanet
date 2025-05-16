namespace Libplanet.Serialization.Converters;

internal sealed class ByteTypeConverter : InternalTypeConverterBase<byte>
{
    protected override byte ConvertFromValue(byte[] value) => value[0];

    protected override byte[] ConvertToValue(byte value) => [value];
}
