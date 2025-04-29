namespace Libplanet.Serialization.Converters;

internal sealed class ByteTypeConverter : InternalTypeConverterBase<byte, Bencodex.Types.Integer>
{
    protected override byte ConvertFromValue(Bencodex.Types.Integer value) => checked((byte)value.Value);

    protected override Bencodex.Types.Integer ConvertToValue(byte value) => new(value);
}
