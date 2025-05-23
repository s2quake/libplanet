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

    public bool IsEnd => Position == Length;

    public int Position { get; init; }

    public byte Current => _bytes[Position];

    public Nibbles NextNibbles => this[Position..];

    public byte this[Index index] => Bytes[index];

    public Nibbles this[Range range]
    {
        get
        {
            var (position, length) = range.GetOffsetAndLength(Bytes.Length);
            if (position < 0 || position > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            if (length < 0 || position + length > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            var builder = ImmutableArray.CreateBuilder<byte>(length);
            for (var i = 0; i < length; i++)
            {
                builder.Add(_bytes[position + i]);
            }

            return new Nibbles(builder.ToImmutable());
        }
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

    public Nibbles Next(int offset) => offset < 0
        ? throw new ArgumentOutOfRangeException(nameof(offset))
        : new Nibbles(_bytes) { Position = Position + offset };

    public Nibbles Next(int position, in Nibbles nibbles)
    {
        var minLength = Math.Min(Length - position, nibbles.Length);
        var count = 0;

        for (var i = 0; i < minLength; i++)
        {
            if (_bytes[i + position] != nibbles[i])
            {
                break;
            }

            count++;
        }

        return Next(count);
    }

    public Nibbles Append(byte @byte) => new(Bytes.Add(@byte));

    public Nibbles Append(in Nibbles nibbles) => new(Bytes.AddRange(nibbles.Bytes));

    public string ToKey()
    {
        var length = Length;
        if (length % 2 != 0)
        {
            throw new InvalidOperationException($"The length of nibbles must be even. Length: {length}");
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

        return code ^ Position;
    }

    public override string ToString()
    {
        var chars = new char[Length];
        var bytes = Bytes;
        for (var i = 0; i < Length; i++)
        {
            chars[i] = _hexCharLookup[bytes[i]];
        }

        var s = new string(chars);
        if (Position < Length)
        {
            s = s.Insert(Position + 1, "\u0332");
        }

        return s;
    }

    public string ToString(string? format, IFormatProvider? formatProvider) => format switch
    {
        "h" => ToString(),
        "H" => ToString().ToUpperInvariant(),
        _ => ToString(),
    };
}
