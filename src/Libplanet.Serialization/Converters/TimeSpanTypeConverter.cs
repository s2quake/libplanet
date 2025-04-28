namespace Libplanet.Serialization.Converters;

internal sealed class TimeSpanTypeConverter : TypeConverterBase<TimeSpan, Bencodex.Types.Integer>
{
    protected override TimeSpan ConvertFromValue(Bencodex.Types.Integer value)
        => new(checked((long)value.Value));

    protected override Bencodex.Types.Integer ConvertToValue(TimeSpan value) => new(value.Ticks);
}
