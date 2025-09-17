using System.ComponentModel;
using Libplanet.Serialization;
using Libplanet.Types.Converters;
using Libplanet.Types.ModelConverters;

namespace Libplanet.Types;

[TypeConverter(typeof(ActionBytecodeTypeConverter))]
[ModelConverter(typeof(ActionBytecodeModelConverter), "action")]
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
}
