namespace Libplanet.Serialization.Converters;

internal sealed class Int32TypeConverter : InternalTypeConverterBase<int, Bencodex.Types.Integer>
{
    protected override int ConvertFromValue(Bencodex.Types.Integer value) => checked((int)value.Value);

    protected override Bencodex.Types.Integer ConvertToValue(int value) => new(value);
}
