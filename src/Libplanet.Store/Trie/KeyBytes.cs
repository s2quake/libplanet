using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Libplanet.Common;

namespace Libplanet.Store.Trie;

public readonly struct KeyBytes(in ImmutableArray<byte> bytes) : IEquatable<KeyBytes>, IFormattable
{
    public static readonly KeyBytes Empty = default;

    private readonly ImmutableArray<byte> _bytes = bytes;

    public int Length => ByteArray.Length;

    public ImmutableArray<byte> ByteArray => _bytes.IsDefault ? [] : _bytes;

    public static explicit operator KeyBytes(string str)
    {
        if (str.Length is 0)
        {
            return Empty;
        }

        return new KeyBytes(CreateArray(str));
    }

    public static bool operator ==(KeyBytes left, KeyBytes right) => left.Equals(right);

    public static bool operator !=(KeyBytes left, KeyBytes right) => !left.Equals(right);

    public static KeyBytes Parse(string hex) => new(ByteUtil.ParseHexToImmutable(hex));

    public static KeyBytes Create(byte[] bytes)
        => bytes.Length is 0 ? Empty : new(ImmutableArray.Create(bytes));

    public byte[] ToByteArray() => [.. ByteArray];

    public ReadOnlySpan<byte> AsSpan() => ByteArray.AsSpan();

    public bool Equals(KeyBytes other) => ByteArray.SequenceEqual(other.ByteArray);

    public override bool Equals(object? obj) => obj is KeyBytes other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        var bytes = ByteArray;
        foreach (byte @byte in bytes)
        {
            hash = unchecked(hash * (31 + @byte));
        }

        return hash;
    }

    public override string ToString() => ByteUtil.Hex(ByteArray);

    public string ToString(string? format, IFormatProvider? formatProvider) => format switch
    {
        "h" => ByteUtil.Hex(ByteArray),
        "H" => ByteUtil.Hex(ByteArray).ToUpperInvariant(),
        _ => ToString(),
    };

    private static ImmutableArray<byte> CreateArray(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        return Unsafe.As<byte[], ImmutableArray<byte>>(ref bytes);
    }
}
