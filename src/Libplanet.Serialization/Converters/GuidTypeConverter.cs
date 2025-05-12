namespace Libplanet.Serialization.Converters;

internal sealed class GuidTypeConverter : InternalTypeConverterBase<Guid, Bencodex.Types.Binary>
{
    protected override Guid ConvertFromValue(Bencodex.Types.Binary value) => new([.. value]);

    protected override Bencodex.Types.Binary ConvertToValue(Guid value) => new(value.ToByteArray());
}
