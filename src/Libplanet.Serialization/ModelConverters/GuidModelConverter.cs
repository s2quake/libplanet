namespace Libplanet.Serialization.ModelConverters;

internal sealed class GuidTypeConverter : InternalModelConverterBase<Guid>
{
    protected override Guid ConvertFromValue(byte[] value) => new(value);

    protected override byte[] ConvertToValue(Guid value) => value.ToByteArray();
}
