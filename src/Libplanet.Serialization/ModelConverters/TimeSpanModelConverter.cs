namespace Libplanet.Serialization.ModelConverters;

internal sealed class TimeSpanTypeConverter : InternalModelConverterBase<TimeSpan>
{
    protected override TimeSpan ConvertFromValue(byte[] value)
        => new(BitConverter.ToInt64(value, 0));

    protected override byte[] ConvertToValue(TimeSpan value) => BitConverter.GetBytes(value.Ticks);
}
