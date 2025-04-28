namespace Libplanet.Serialization.Converters;

internal sealed class DateTimeOffsetTypeConverter : InternalTypeConverterBase<DateTimeOffset, Bencodex.Types.Integer>
{
    protected override DateTimeOffset ConvertFromValue(Bencodex.Types.Integer value)
        => new(checked((long)value.Value), TimeSpan.Zero);

    protected override Bencodex.Types.Integer ConvertToValue(DateTimeOffset value) => new(value.UtcTicks);
}
