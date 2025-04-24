using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Serialization;
using Libplanet.Types.JsonConverters;

namespace Libplanet.Types.Tx;

[JsonConverter(typeof(TxIdJsonConverter))]
[Model(Version = 1)]
public readonly record struct TxId(in ImmutableArray<byte> ByteArray)
    : IEquatable<TxId>, IComparable<TxId>, IComparable
{
    public const int Size = 32;

    private static readonly ImmutableArray<byte> _defaultByteArray
        = ImmutableArray.Create(new byte[Size]);

    private readonly ImmutableArray<byte> _bytes = ValidateBytes(ByteArray);

    public TxId(ReadOnlySpan<byte> bytes)
        : this(bytes.ToImmutableArray())
    {
    }

    public ImmutableArray<byte> ByteArray => _bytes.IsDefault ? _defaultByteArray : _bytes;

    public IValue ToBencodex() => new Binary(ByteArray);

    public static TxId Parse(string hex)
    {
        try
        {
            return new TxId(ByteUtil.ParseHexToImmutable(hex));
        }
        catch (Exception e) when (e is not FormatException)
        {
            throw new FormatException(
                $"Given {nameof(hex)} must be a hexadecimal string: {e.Message}", e);
        }
    }

    public static TxId Create(IValue bencoded)
    {
        if (bencoded is Binary binary)
        {
            return new TxId(binary.ByteArray);
        }

        var message = $"Given {nameof(bencoded)} must be of type " +
                      $"{typeof(Binary)}: {bencoded.GetType()}";
        throw new ArgumentException(message, nameof(bencoded));
    }

    public bool Equals(TxId other) => ByteArray.SequenceEqual(other.ByteArray);

    public override int GetHashCode() => ByteUtil.CalculateHashCode(ByteArray);

    public override string ToString() => ByteUtil.Hex(ByteArray);

    public int CompareTo(TxId other)
    {
        for (var i = 0; i < Size; ++i)
        {
            var cmp = ByteArray[i].CompareTo(other.ByteArray[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    public int CompareTo(object? obj)
    {
        if (obj is not TxId other)
        {
            throw new ArgumentException(
                $"Argument {nameof(obj)} is not a ${nameof(TxId)}.", nameof(obj));
        }

        return CompareTo(other);
    }

    private static ImmutableArray<byte> ValidateBytes(in ImmutableArray<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytes), $"Given {nameof(bytes)} must be {Size} bytes.");
        }

        return bytes;
    }
}
