using System.Runtime.CompilerServices;
using System.Text;
using Libplanet.Common;

namespace Libplanet.Store.Trie;

public readonly record struct KeyBytes(in ImmutableArray<byte> Bytes)
    : IEquatable<KeyBytes>, IFormattable
{
    public static readonly KeyBytes Empty = default;

    private readonly ImmutableArray<byte> _bytes = Bytes;

    public int Length => Bytes.Length;

    public ImmutableArray<byte> Bytes => _bytes.IsDefault ? [] : _bytes;

    public static explicit operator KeyBytes(string str)
    {
        if (str.Length is 0)
        {
            return Empty;
        }

        return new KeyBytes(CreateArray(str));
    }

    public static KeyBytes Parse(string hex) => new(ByteUtil.ParseHexToImmutable(hex));

    public static KeyBytes Create(byte[] bytes)
        => bytes.Length is 0 ? Empty : new(ImmutableArray.Create(bytes));

    public byte[] ToByteArray() => [.. Bytes];

    public ReadOnlySpan<byte> AsSpan() => Bytes.AsSpan();

    public bool Equals(KeyBytes other) => Bytes.SequenceEqual(other.Bytes);

    public override int GetHashCode()
    {
        var hash = 17;
        var bytes = Bytes;
        foreach (byte @byte in bytes)
        {
            hash = unchecked(hash * (31 + @byte));
        }

        return hash;
    }

    public override string ToString() => ByteUtil.Hex(Bytes);

    public string ToString(string? format, IFormatProvider? formatProvider) => format switch
    {
        "h" => ByteUtil.Hex(Bytes),
        "H" => ByteUtil.Hex(Bytes).ToUpperInvariant(),
        _ => ToString(),
    };

    private static ImmutableArray<byte> CreateArray(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        return Unsafe.As<byte[], ImmutableArray<byte>>(ref bytes);
    }
}
