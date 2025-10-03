using Libplanet.Serialization;

namespace Libplanet.Types;

[ModelScalar("action", Kind = ModelScalarKind.Hex)]
public readonly record struct ActionBytecode(in ImmutableArray<byte> Bytes) : IEquatable<ActionBytecode>
{
    public ActionBytecode(ReadOnlySpan<byte> bytes)
        : this(bytes.ToImmutableArray())
    {
    }

    public bool Equals(ActionBytecode other)
    {
        if (other.Bytes == default && Bytes == default)
        {
            return true;
        }

        if (other.Bytes == default || Bytes == default)
        {
            return false;
        }

        return Bytes.SequenceEqual(other.Bytes);
    }

    public override int GetHashCode() => ByteUtility.GetHashCode(Bytes);

    internal byte[] ToScalarValue() => [.. Bytes];

    internal static ActionBytecode FromScalarValue(IServiceProvider serviceProvider, byte[] value)
        => new(value.ToImmutableArray());
}
