#if !NETSTANDARD2_0
using System.Buffers;
#endif
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Libplanet.Types;

public static class ByteUtility
{
    private static readonly char[] _hexCharLookup =
    [
        '0', '1', '2', '3', '4', '5', '6', '7',
        '8', '9', 'a', 'b', 'c', 'd', 'e', 'f',
    ];

    public static byte[] ParseHex(string hex)
    {
        if (hex.Length % 2 > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hex),
                "A length of a hexadecimal string must be an even number.");
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < hex.Length / 2; i++)
        {
            bytes[i] = byte.Parse(
                hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return bytes;
    }

    public static ImmutableArray<byte> ParseHexToImmutable(string hex)
    {
        var bytes = ParseHex(hex);
        return Unsafe.As<byte[], ImmutableArray<byte>>(ref bytes);
    }

    public static string Hex(in ImmutableArray<byte> bytes) => Hex(bytes.AsSpan());

    public static string Hex(ReadOnlySpan<byte> bytes)
    {
#if NETSTANDARD2_0
        char[] chars = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = _hexCharLookup[bytes[i] >> 4];
            chars[i * 2 + 1] = _hexCharLookup[bytes[i] & 0xf];
        }

        return new string(chars);
#else
        int length = bytes.Length * 2;
        char[] chars = ArrayPool<char>.Shared.Rent(length);
        for (int i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = _hexCharLookup[bytes[i] >> 4];
            chars[(i * 2) + 1] = _hexCharLookup[bytes[i] & 0xf];
        }

        string result = new string(chars, 0, length);
        ArrayPool<char>.Shared.Return(chars);
        return result;
#endif
    }

    public static int GetHashCode(in ImmutableArray<byte> bytes) => GetHashCode(bytes.AsSpan());

    public static int GetHashCode(ReadOnlySpan<byte> bytes)
    {
        HashCode code = new();
        code.AddBytes(bytes);
        return code.ToHashCode();
    }

    public static bool TimingSafelyCompare(IReadOnlyList<byte> left, IReadOnlyList<byte> right)
    {
        bool differ = left.Count != right.Count;
        for (int i = 0, len = Math.Min(left.Count, right.Count); i < len; i++)
        {
            differ = differ || (left[i] ^ right[i]) != 0;
        }

        return !differ;
    }

    public static bool Satisfies(IReadOnlyList<byte> hashDigest, long difficulty)
    {
        if (difficulty == 0)
        {
            return true;
        }
        else if (!hashDigest.Any())
        {
            return false;
        }

        var maxTargetBytes = new byte[hashDigest.Count + 1];
        maxTargetBytes[hashDigest.Count] = 0x01;
        var maxTarget = new BigInteger(maxTargetBytes);
        BigInteger target = maxTarget / difficulty;

        var digestArray = new byte[hashDigest.Count + 1];
        int i = 0;
        foreach (byte b in hashDigest)
        {
            digestArray[i++] = b;
        }

        // Append zero to convert unsigned BigInteger.  Note that BigInteger(byte[]) assumes
        // the input bytes are in little-endian order.
        digestArray[i] = 0;

        var result = new BigInteger(digestArray);
        return result < target;
    }
}
