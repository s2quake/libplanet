namespace Libplanet.Serialization.Converters;

internal sealed class BooleanTypeConverter : InternalTypeConverterBase<bool>
{
    protected override bool ConvertFromValue(byte[] value) => BitConverter.ToBoolean(value, 0);

    protected override byte[] ConvertToValue(bool value) => BitConverter.GetBytes(value);
}
