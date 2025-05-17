namespace Libplanet.Serialization.ModelConverters;

internal sealed class DateTimeOffsetTypeConverter : InternalModelConverterBase<DateTimeOffset>
{
    protected override DateTimeOffset ConvertFromValue(byte[] value)
        => new(BitConverter.ToInt64(value, 0), TimeSpan.Zero);

    protected override byte[] ConvertToValue(DateTimeOffset value)
        => BitConverter.GetBytes(value.UtcTicks);
}
