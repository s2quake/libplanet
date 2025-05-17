namespace Libplanet.Serialization.ModelConverters;

internal sealed class ByteTypeConverter : InternalModelConverterBase<byte>
{
    protected override byte ConvertFromValue(byte[] value) => value[0];

    protected override byte[] ConvertToValue(byte value) => [value];
}
