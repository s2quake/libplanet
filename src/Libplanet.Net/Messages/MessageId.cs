using Libplanet.Types;

namespace Libplanet.Net.Messages;

public readonly record struct MessageId(in ImmutableArray<byte> Bytes)
    : IEquatable<MessageId>, IComparable<MessageId>, IComparable
{
    public const int Size = 32;

    private static readonly ImmutableArray<byte> _defaultByteArray = ImmutableArray.Create(new byte[Size]);

    private readonly ImmutableArray<byte> _bytes = ValidateBytes(Bytes);

    public MessageId(ReadOnlySpan<byte> bytes)
        : this(bytes.ToImmutableArray())
    {
    }

    public ImmutableArray<byte> Bytes => _bytes.IsDefault ? _defaultByteArray : _bytes;

    public static MessageId Parse(string hex)
    {
        try
        {
            return new MessageId(ByteUtility.ParseHexToImmutable(hex));
        }
        catch (Exception e) when (e is not FormatException)
        {
            throw new FormatException(
                $"Given {nameof(hex)} must be a hexadecimal string: {e.Message}", e);
        }
    }

    public bool Equals(MessageId other) => Bytes.SequenceEqual(other.Bytes);

    public override int GetHashCode() => ByteUtility.GetHashCode(Bytes);

    public override string ToString() => ByteUtility.Hex(Bytes);

    public int CompareTo(MessageId other)
    {
        for (var i = 0; i < Size; ++i)
        {
            var cmp = Bytes[i].CompareTo(other.Bytes[i]);
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
        MessageId other => CompareTo(other),
        _ => throw new ArgumentException($"Argument {nameof(obj)} is not ${nameof(MessageId)}.", nameof(obj)),
    };

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
