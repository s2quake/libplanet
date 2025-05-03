#if !NETSTANDARD2_0
using System.Buffers;
#endif
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Bencodex.Json;
using Bencodex.Types;

namespace Libplanet.Types;

public static class ByteUtility
{
    private static readonly char[] _hexCharLookup =
    [
        '0', '1', '2', '3', '4', '5', '6', '7',
        '8', '9', 'a', 'b', 'c', 'd', 'e', 'f',
    ];

    private static readonly BencodexJsonConverter BencodexJsonConverter = new();
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            BencodexJsonConverter,
        },
    };

    public static byte[] ParseHex(string hex)
    {
        if (hex.Length % 2 > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hex),
                "A length of a hexadecimal string must be an even number."
            );
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
        byte[] bytes = ParseHex(hex);
        ImmutableArray<byte> movedImmutableArray =
            Unsafe.As<byte[], ImmutableArray<byte>>(ref bytes);
        return movedImmutableArray;
    }

    public static string Hex(byte[] bytes)
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
            chars[i * 2 + 1] = _hexCharLookup[bytes[i] & 0xf];
        }

        string result = new string(chars, 0, length);
        ArrayPool<char>.Shared.Return(chars);
        return result;
#endif
    }

    public static string Hex(in ImmutableArray<byte> bytes) => Hex(bytes.IsDefaultOrEmpty ? [] : [.. bytes]);

    public static int CalculateHashCode(byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        int code = 0;
        unchecked
        {
            foreach (byte b in bytes)
            {
                code = (code * 397) ^ b.GetHashCode();
            }
        }

        return code;
    }

    public static int CalculateHashCode(in ImmutableArray<byte> bytes)
    {
        if (bytes.IsDefaultOrEmpty)
        {
            return 0;
        }

        var code = 0;
        unchecked
        {
            foreach (var @byte in bytes)
            {
                code = (code * 397) ^ @byte.GetHashCode();
            }
        }

        return code;
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

    public static byte[] CreateMessage(IValue value)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
        BencodexJsonConverter.Write(writer, value, SerializerOptions);
        ms.Position = 0;
        using var sr = new StreamReader(ms);
        return Encoding.UTF8.GetBytes(sr.ReadToEnd());
    }
}
