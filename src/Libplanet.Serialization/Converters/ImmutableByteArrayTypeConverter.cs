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

        if (value is Null)
        {
            return default!;
        }

        throw new NotSupportedException(
            $"Cannot convert from {value.GetType()} to {typeof(ImmutableArray<byte>)}.");
    }

    protected override IValue ConvertToValue(ImmutableArray<byte> value)
    {
        if (value.IsDefault)
        {
            return Null.Value;
        }

        return new Binary(value.ToArray());
    }
}
