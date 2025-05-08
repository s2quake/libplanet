using Bencodex.Types;

namespace Libplanet.Serialization.Converters;

internal sealed class ImmutableByteArrayTypeConverter : InternalTypeConverterBase<ImmutableArray<byte>, IValue>
{
    protected override ImmutableArray<byte> ConvertFromValue(IValue value)
    {
        if (value is Binary binary)
        {
            return [.. binary];
        }

        throw new NotSupportedException(
            $"Cannot convert from {value.GetType()} to {typeof(ImmutableArray<byte>)}.");
    }

    protected override IValue ConvertToValue(ImmutableArray<byte> value)
    {
        return new Binary(value.ToArray());
    }
}
