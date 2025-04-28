namespace Libplanet.Serialization.Converters;

internal sealed class Int64TypeConverter : TypeConverterBase<long, Bencodex.Types.Integer>
{
    protected override long ConvertFromValue(Bencodex.Types.Integer value) => checked((long)value.Value);

    protected override Bencodex.Types.Integer ConvertToValue(long value) => new(value);
}
