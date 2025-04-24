using System.Text.Json.Serialization;
using Libplanet.Common;
using Libplanet.Types.JsonConverters;

namespace Libplanet.Types.Evidence;

[JsonConverter(typeof(EvidenceIdJsonConverter))]
public readonly record struct EvidenceId(in ImmutableArray<byte> ByteArray)
    : IEquatable<EvidenceId>, IComparable<EvidenceId>, IComparable
{
    public const int Size = 32;

    private static readonly ImmutableArray<byte> _defaultByteArray
        = ImmutableArray.Create(new byte[Size]);

    private readonly ImmutableArray<byte> _bytes = ByteArray;

    public EvidenceId(ReadOnlySpan<byte> bytes)
        : this(bytes.ToImmutableArray())
    {
    }

    public static ImmutableArray<byte> DefaultByteArray => _defaultByteArray;

    public ImmutableArray<byte> ByteArray => _bytes.IsDefault ? DefaultByteArray : _bytes;

    public static EvidenceId Parse(string hex)
    {
        ImmutableArray<byte> bytes = ByteUtil.ParseHexToImmutable(hex);
        try
        {
            return new EvidenceId(bytes);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hex),
                $"Expected {Size * 2} characters, but {hex.Length} characters given.");
        }
    }

    public static bool TryParse(string hex, out EvidenceId? evidenceId)
    {
        try
        {
            evidenceId = Parse(hex);
            return true;
        }
        catch (Exception)
        {
            evidenceId = null;
            return false;
        }
    }

    public bool Equals(EvidenceId other) => ByteArray.SequenceEqual(other.ByteArray);

    public override int GetHashCode() => ByteUtil.CalculateHashCode(ByteArray);

    public override string ToString() => ByteUtil.Hex(ByteArray);

    public int CompareTo(EvidenceId other)
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
        if (obj is not EvidenceId other)
        {
            throw new ArgumentException(
                $"Argument {nameof(obj)} is not a ${nameof(EvidenceId)}.", nameof(obj));
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
