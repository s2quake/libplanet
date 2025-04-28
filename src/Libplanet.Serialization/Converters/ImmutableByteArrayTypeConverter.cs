namespace Libplanet.Serialization.Converters;

internal sealed class ImmutableByteArrayTypeConverter : TypeConverterBase<ImmutableArray<byte>, Bencodex.Types.Binary>
{
    protected override ImmutableArray<byte> ConvertFromValue(Bencodex.Types.Binary value) => [.. value];

    protected override Bencodex.Types.Binary ConvertToValue(ImmutableArray<byte> value) => new(value);
}
