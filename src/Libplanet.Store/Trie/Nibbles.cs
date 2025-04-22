using System;
using System.Collections.Immutable;
using System.Linq;

namespace Libplanet.Store.Trie;

public readonly record struct Nibbles : IEquatable<Nibbles>, IFormattable
{
    public static readonly Nibbles Empty = default;

    private static readonly char[] _hexCharLookup =
    [
        '0', '1', '2', '3', '4', '5', '6', '7',
        '8', '9', 'a', 'b', 'c', 'd', 'e', 'f',
    ];

    private readonly ImmutableArray<byte> _bytes;

    internal Nibbles(in ImmutableArray<byte> bytes) => _bytes = bytes;

    public ImmutableArray<byte> ByteArray => _bytes.IsDefault ? [] : _bytes;

    public int Length => ByteArray.Length;

    public byte this[int index] => ByteArray[index];

    public static Nibbles Parse(string hex)
    {
        var builder = ImmutableArray.CreateBuilder<byte>(hex.Length);
        for (var i = 0; i < hex.Length; i++)
        {
            builder.Add((byte)Uri.FromHex(hex[i]));
        }

        return new Nibbles(builder.ToImmutable());
    }

    public static Nibbles FromKeyBytes(in KeyBytes keyBytes)
    {
        var capacity = keyBytes.ByteArray.Length * 2;
        var builder = ImmutableArray.CreateBuilder<byte>(capacity);
        for (var i = 0; i < keyBytes.ByteArray.Length; i++)
        {
            builder.Add((byte)(keyBytes.ByteArray[i] >> 4));
            builder.Add((byte)(keyBytes.ByteArray[i] & 0x0f));
        }

        return new Nibbles(builder.ToImmutable());
    }

    public Nibbles Append(byte @byte) => new(ByteArray.Add(@byte));

    public Nibbles Append(in Nibbles nibbles) => new(ByteArray.AddRange(nibbles.ByteArray));

    public Nibbles AppendMany(in ImmutableArray<byte> bytes) => new(ByteArray.AddRange(bytes));

    public Nibbles Take(int count)
    {
        if (count < 0)
        {
            var message = $"Given {nameof(count)} must be non-negative: {count}";
            throw new ArgumentOutOfRangeException(nameof(count), message);
        }

        if (count > Length)
        {
            var message = $"Given {nameof(count)} must be less than or equal to {Length}: {count}";
            throw new ArgumentOutOfRangeException(nameof(count), message);
        }

        return new Nibbles([.. _bytes.Take(count)]);
    }

    public Nibbles Take(in Nibbles nibbles)
    {
        var minLength = Math.Min(Length, nibbles.Length);
        var builder = ImmutableArray.CreateBuilder<byte>(minLength);

        for (var i = 0; i < minLength; i++)
        {
            if (ByteArray[i] != nibbles.ByteArray[i])
            {
                break;
            }

            builder.Add(ByteArray[i]);
        }

        return new Nibbles(builder.ToImmutable());
    }

    public Nibbles Skip(int count)
    {
        if (count < 0)
        {
            var message = $"Given {nameof(count)} must be non-negative: {count}";
            throw new ArgumentOutOfRangeException(nameof(count), message);
        }

        if (count > Length)
        {
            var message = $"Given {nameof(count)} must be less than or equal to {Length}: {count}";
            throw new ArgumentOutOfRangeException(nameof(count), message);
        }

        return new Nibbles([.. _bytes.Skip(count)]);
    }

    public KeyBytes ToKeyBytes()
    {
        var length = Length;
        if (length % 2 != 0)
        {
            throw new InvalidOperationException(
                $"The length must be even in order to convert " +
                $"to a {nameof(KeyBytes)}: {length}");
        }

        var capacity = length / 2;
        var builder = ImmutableArray.CreateBuilder<byte>(capacity);
        for (var i = 0; i < length; i += 2)
        {
            builder.Add((byte)(_bytes[i] << 4 | _bytes[i + 1]));
        }

        return new KeyBytes(builder.ToImmutable());
    }

    public bool StartsWith(in Nibbles nibbles)
    {
        if (Length < nibbles.Length)
        {
            return false;
        }

        for (var i = 0; i < nibbles.Length; i++)
        {
            if (ByteArray[i] != nibbles.ByteArray[i])
            {
                return false;
            }
        }

        return true;
    }

    public bool Equals(Nibbles other) => ByteArray.SequenceEqual(other.ByteArray);

    public override int GetHashCode()
    {
        var code = 0;
        unchecked
        {
            var bytes = ByteArray;
            foreach (byte @byte in bytes)
            {
                code = (code * 397) ^ @byte.GetHashCode();
            }
        }

        return code;
    }

    public override string ToString()
    {
        var chars = new char[Length];
        var bytes = ByteArray;
        for (var i = 0; i < Length; i++)
        {
            chars[i] = _hexCharLookup[bytes[i]];
        }

        return new string(chars);
    }

    public string ToString(string? format, IFormatProvider? formatProvider) => format switch
    {
        "h" => ToString(),
        "H" => ToString().ToUpperInvariant(),
        _ => ToString(),
    };
}
