using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Libplanet.Types.Converters;
using Libplanet.Types.JsonConverters;

namespace Libplanet.Types.Blocks;

[JsonConverter(typeof(BlockHashJsonConverter))]
[TypeConverter(typeof(BlockHashTypeConverter))]
public readonly record struct BlockHash(in ImmutableArray<byte> Bytes)
    : IEquatable<BlockHash>, IComparable<BlockHash>, IComparable, IFormattable
{
    public const int Size = 32;

    private static readonly ImmutableArray<byte> _defaultBytes = new byte[Size].ToImmutableArray();

    private readonly ImmutableArray<byte> _bytes = ValidateBytes(Bytes);

    public BlockHash(ReadOnlySpan<byte> bytes)
        : this(bytes.ToImmutableArray())
    {
    }

    public ImmutableArray<byte> Bytes => _bytes.IsDefault ? _defaultBytes : _bytes;

    public static BlockHash Parse(string hex)
    {
        try
        {
            return new(ByteUtility.ParseHexToImmutable(hex));
        }
        catch (Exception e)
        {
            throw new FormatException(
                $"The string '{hex}' is not a valid {nameof(BlockHash)}.", e);
        }
    }

    public static BlockHash Create(HashDigest<SHA256> hashDigest) => new(hashDigest.Bytes);

    public static BlockHash Create(ReadOnlySpan<byte> bytes) => new(SHA256.HashData(bytes));

    public bool Equals(BlockHash other)
    {
        if (_bytes.IsDefaultOrEmpty && other._bytes.IsDefaultOrEmpty)
        {
            return true;
        }
        else if (Bytes.Length != other.Bytes.Length)
        {
            return false;
        }

        for (int i = 0; i < Bytes.Length; i++)
        {
            if (!Bytes[i].Equals(other.Bytes[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var code = 0;
        unchecked
        {
            var bytes = Bytes;
            foreach (var @byte in bytes)
            {
                code = (code * 397) ^ @byte.GetHashCode();
            }
        }

        return code;
    }

    public int CompareTo(BlockHash other)
    {
        ImmutableArray<byte> self = Bytes, operand = other.Bytes;

        for (int i = 0; i < Size; i++)
        {
            int cmp = ((IComparable<byte>)self[i]).CompareTo(operand[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        BlockHash other => CompareTo(other),
        _ => throw new ArgumentException($"Argument {nameof(obj)} is not ${nameof(BlockHash)}.", nameof(obj)),
    };

    public override string ToString() => ToString("h", null);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var hex = ByteUtility.Hex(Bytes);
        return format switch
        {
            "h" => hex,
            "H" => hex.ToUpperInvariant(),
            null => hex,
            _ => throw new FormatException($"The format string '{format}' is not supported."),
        };
    }

    private static ImmutableArray<byte> ValidateBytes(in ImmutableArray<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            var message = $"{nameof(BlockHash)} must be {Size} bytes, " +
                          $"but {bytes.Length} was given.";
            throw new ArgumentOutOfRangeException(nameof(bytes), message);
        }

        return bytes;
    }
}
