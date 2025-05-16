namespace Libplanet.Serialization.Converters;

internal sealed class GuidTypeConverter : InternalTypeConverterBase<Guid>
{
    protected override Guid ConvertFromValue(byte[] value) => new(value);

    protected override byte[] ConvertToValue(Guid value) => value.ToByteArray();
}
