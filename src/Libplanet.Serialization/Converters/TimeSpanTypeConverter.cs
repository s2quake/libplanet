namespace Libplanet.Serialization.Converters;

internal sealed class TimeSpanTypeConverter : InternalTypeConverterBase<TimeSpan>
{
    protected override TimeSpan ConvertFromValue(byte[] value)
        => new(BitConverter.ToInt64(value, 0));

    protected override byte[] ConvertToValue(TimeSpan value) => BitConverter.GetBytes(value.Ticks);
}
