namespace Libplanet.Serialization.Converters;

internal sealed class Int32TypeConverter : InternalTypeConverterBase<int>
{
    protected override int ConvertFromValue(byte[] value) => BitConverter.ToInt32(value, 0);

    protected override byte[] ConvertToValue(int value) => BitConverter.GetBytes(value);
}
