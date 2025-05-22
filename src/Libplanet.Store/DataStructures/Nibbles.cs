using System.Text;
using Libplanet.Serialization;
using Libplanet.Store.ModelConverters;

namespace Libplanet.Store.DataStructures;

[ModelConverter(typeof(NibblesModelConverter))]
internal readonly record struct Nibbles : IEquatable<Nibbles>, IFormattable
{
    public static readonly Nibbles Empty = default;

    private static readonly char[] _hexCharLookup =
    [
        '0', '1', '2', '3', '4', '5', '6', '7',
        '8', '9', 'a', 'b', 'c', 'd', 'e', 'f',
    ];

    private readonly ImmutableArray<byte> _bytes;

    internal Nibbles(in ImmutableArray<byte> bytes) => _bytes = bytes;

    public ImmutableArray<byte> Bytes => _bytes.IsDefault ? [] : _bytes;

    public int Length => Bytes.Length;

    public byte this[int index] => Bytes[index];

    public static Nibbles Parse(string hex)
    {
        var builder = ImmutableArray.CreateBuilder<byte>(hex.Length);
        for (var i = 0; i < hex.Length; i++)
        {
            builder.Add((byte)Uri.FromHex(hex[i]));
        }

        return new Nibbles(builder.ToImmutable());
    }

    public static Nibbles Create(string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var capacity = keyBytes.Length * 2;
        var builder = ImmutableArray.CreateBuilder<byte>(capacity);
        for (var i = 0; i < keyBytes.Length; i++)
        {
            builder.Add((byte)(keyBytes[i] >> 4));
            builder.Add((byte)(keyBytes[i] & 0x0f));
        }

        return new Nibbles(builder.ToImmutable());
    }

    public Nibbles Append(byte @byte) => new(Bytes.Add(@byte));

    public Nibbles Append(in Nibbles nibbles) => new(Bytes.AddRange(nibbles.Bytes));

    public Nibbles AppendMany(in ImmutableArray<byte> bytes) => new(Bytes.AddRange(bytes));

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
            if (Bytes[i] != nibbles.Bytes[i])
            {
                break;
            }

            builder.Add(Bytes[i]);
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

    public string ToKey()
    {
        var length = Length;
        if (length % 2 != 0)
        {
            throw new InvalidOperationException("");
        }

        var capacity = length / 2;
        Span<byte> bytes = stackalloc byte[capacity];
        for (var i = 0; i < length; i += 2)
        {
            bytes[i / 2] = (byte)(_bytes[i] << 4 | _bytes[i + 1]);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    public bool StartsWith(in Nibbles nibbles)
    {
        if (Length < nibbles.Length)
        {
            return false;
        }

        for (var i = 0; i < nibbles.Length; i++)
        {
            if (Bytes[i] != nibbles.Bytes[i])
            {
                return false;
            }
        }

        return true;
    }

    public bool Equals(Nibbles other) => Bytes.SequenceEqual(other.Bytes);

    public override int GetHashCode()
    {
        var code = 0;
        unchecked
        {
            var bytes = Bytes;
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
        var bytes = Bytes;
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
