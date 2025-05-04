namespace Libplanet.Types.Tx;

public readonly record struct ActionBytecode(in ImmutableArray<byte> Bytes) : IEquatable<ActionBytecode>
{
    public bool Equals(ActionBytecode other) => Bytes.SequenceEqual(other.Bytes);

    public override int GetHashCode() => ByteUtility.CalculateHashCode(Bytes);
}