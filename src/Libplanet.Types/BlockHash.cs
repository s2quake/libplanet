using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Libplanet.Serialization;
using Libplanet.Types.Converters;
using Libplanet.Types.JsonConverters;
using Libplanet.Types.ModelConverters;

namespace Libplanet.Types;

[JsonConverter(typeof(BlockHashJsonConverter))]
[TypeConverter(typeof(BlockHashTypeConverter))]
[ModelConverter(typeof(BlockHashModelConverter), "blhs")]
public readonly partial record struct BlockHash(in ImmutableArray<byte> Bytes)
    : IEquatable<BlockHash>, IComparable<BlockHash>, IComparable, IFormattable
{
    public const int Size = 32;

    private static readonly ImmutableArray<byte> _defaultBytes = ImmutableArray.Create(new byte[Size]);

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

    public static BlockHash HashData(ReadOnlySpan<byte> bytes) => new(SHA256.HashData(bytes));

    public bool Equals(BlockHash other) => Bytes.SequenceEqual(other.Bytes);

    public override int GetHashCode() => ByteUtility.GetHashCode(Bytes);

    public int CompareTo(BlockHash other)
    {
        var self = Bytes;
        var operand = other.Bytes;

        for (var i = 0; i < Size; i++)
        {
            var cmp = self[i].CompareTo(operand[i]);
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
