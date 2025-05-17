using System.ComponentModel;
using Libplanet.Serialization;
using Libplanet.Types.Converters;
using Libplanet.Types.ModelConverters;

namespace Libplanet.Types.Tx;

[TypeConverter(typeof(ActionBytecodeTypeConverter))]
[ModelConverter(typeof(ActionBytecodeModelConverter))]
public readonly record struct ActionBytecode(in ImmutableArray<byte> Bytes) : IEquatable<ActionBytecode>
{
    public ActionBytecode(ReadOnlySpan<byte> bytes)
        : this(bytes.ToImmutableArray())
    {
    }

    public bool Equals(ActionBytecode other) => Bytes.SequenceEqual(other.Bytes);

    public override int GetHashCode() => ByteUtility.CalculateHashCode(Bytes);
}
