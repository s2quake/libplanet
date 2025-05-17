namespace Libplanet.Serialization.ModelConverters;

internal sealed class Int64TypeConverter : InternalModelConverterBase<long>
{
    protected override long ConvertFromValue(byte[] value) => BitConverter.ToInt64(value, 0);

    protected override byte[] ConvertToValue(long value) => BitConverter.GetBytes(value);
}
