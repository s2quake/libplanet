using System.ComponentModel;
using Libplanet.Types.Converters;

namespace Libplanet.Types.Tx;

[TypeConverter(typeof(ActionBytecodeTypeConverter))]
public readonly record struct ActionBytecode(in ImmutableArray<byte> Bytes) : IEquatable<ActionBytecode>
{
    public bool Equals(ActionBytecode other) => Bytes.SequenceEqual(other.Bytes);

    public override int GetHashCode() => ByteUtility.CalculateHashCode(Bytes);
}
