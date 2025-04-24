using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Types.Converters;

namespace Libplanet.Types.Blocks;

[JsonConverter(typeof(BlockHashJsonConverter))]
public readonly struct BlockHash :
    IEquatable<BlockHash>, IComparable<BlockHash>, IComparable, IBencodable, IFormattable
{
    public const int Size = 32;

    private static readonly ImmutableArray<byte> _defaultBytes
        = new byte[Size].ToImmutableArray();

    private readonly ImmutableArray<byte> _bytes;

    public BlockHash(in ImmutableArray<byte> immutableBytes)
    {
        if (immutableBytes.Length != Size)
        {
            string message =
                $"{nameof(BlockHash)} must be {Size} bytes, but {immutableBytes.Length} was given.";
            throw new ArgumentOutOfRangeException(nameof(immutableBytes), message);
        }

        _bytes = immutableBytes;
    }

    public BlockHash(byte[] bytes)
        : this(bytes.ToImmutableArray())
    {
    }

    public BlockHash(IValue value)
    {
        if (value is not Binary binary)
        {
            var message = $"Given {nameof(value)} must be of type " +
                          $"{typeof(Binary)}: {value.GetType()}";
            throw new ArgumentException(message, nameof(value));
        }

        var bytes = binary.ByteArray;
        if (bytes.Length != Size)
        {
            var message = $"{nameof(BlockHash)} must be {Size} bytes, " +
                          $"but {bytes.Length} was given";
            throw new ArgumentOutOfRangeException(nameof(value), message);
        }

        _bytes = bytes;
    }

    public ImmutableArray<byte> ByteArray => _bytes.IsDefault ? _defaultBytes : _bytes;

    public IValue Bencoded => new Binary(ByteArray);

    public static bool operator ==(BlockHash left, BlockHash right) => left.Equals(right);

    public static bool operator !=(BlockHash left, BlockHash right) => !left.Equals(right);

    public static BlockHash Parse(string hex) => new(ByteUtil.ParseHexToImmutable(hex));

    public static BlockHash Create(HashDigest<SHA256> hashDigest) => new(hashDigest.ByteArray);

    public static BlockHash DeriveFrom(IReadOnlyList<byte> blockBytes)
    {
        SHA256 sha256 = SHA256.Create();
        byte[] digest = sha256.ComputeHash(blockBytes is byte[] b ? b : blockBytes.ToArray());
        return new BlockHash(digest);
    }

    public byte[] ToByteArray() => [.. ByteArray];

    public bool Equals(BlockHash other)
    {
        if (_bytes.IsDefaultOrEmpty && other._bytes.IsDefaultOrEmpty)
        {
            return true;
        }
        else if (ByteArray.Length != other.ByteArray.Length)
        {
            return false;
        }

        for (int i = 0; i < ByteArray.Length; i++)
        {
            if (!ByteArray[i].Equals(other.ByteArray[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is BlockHash h && Equals(h);

    public override int GetHashCode()
    {
        var code = 0;
        unchecked
        {
            var bytes = ByteArray;
            foreach (var @byte in bytes)
            {
                code = (code * 397) ^ @byte.GetHashCode();
            }
        }

        return code;
    }

    public int CompareTo(BlockHash other)
    {
        ImmutableArray<byte> self = ByteArray, operand = other.ByteArray;

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

    public int CompareTo(object? obj) => obj is BlockHash other
        ? this.CompareTo(other)
        : throw new ArgumentException(
            $"Argument {nameof(obj)} is not an ${nameof(BlockHash)}.", nameof(obj));

    public override string ToString() => ToString("h", null);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var hex = ByteUtil.Hex(ToByteArray());
        return format switch
        {
            "h" => hex,
            "H" => hex.ToUpperInvariant(),
            null => hex,
            _ => throw new FormatException($"The format string '{format}' is not supported."),
        };
    }
}
