namespace Libplanet.Serialization.ModelConverters;

internal sealed class BooleanTypeConverter : InternalModelConverterBase<bool>
{
    protected override bool ConvertFromValue(byte[] value) => BitConverter.ToBoolean(value, 0);

    protected override byte[] ConvertToValue(bool value) => BitConverter.GetBytes(value);
}
