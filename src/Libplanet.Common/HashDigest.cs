using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using Libplanet.Common.JsonConverters;

namespace Libplanet.Common;

[TypeConverter(typeof(HashDigestTypeConverter))]
[JsonConverter(typeof(HashDigestJsonConverter))]
public readonly record struct HashDigest<T>(in ImmutableArray<byte> Bytes)
    : IEquatable<HashDigest<T>>, IFormattable
    where T : HashAlgorithm
{
    public static readonly int Size;

    public static readonly HashDigest<T> Empty = default;

    private static readonly ThreadLocal<T> Algorithm;
    private static readonly ImmutableArray<byte> DefaultByteArray;

    private readonly ImmutableArray<byte> _bytes = ValidateBytes(Bytes);

    static HashDigest()
    {
        var type = typeof(T);
        if (type.GetMethod(nameof(HashAlgorithm.Create), []) is not { } method)
        {
            var message = $"Failed to look up the {nameof(HashAlgorithm.Create)} method";
            throw new UnreachableException(message);
        }

        var methodCall = Expression.Call(null, method);
        var exception = new InvalidCastException($"Failed to invoke {methodCall} static method.");
        var instantiateAlgorithm = Expression.Lambda<Func<T>>(
            Expression.Coalesce(methodCall, Expression.Throw(Expression.Constant(exception), type))
        ).Compile();
        Algorithm = new ThreadLocal<T>(instantiateAlgorithm);
        Size = Algorithm.Value!.HashSize / 8;
        DefaultByteArray = [.. Enumerable.Repeat(default(byte), Size)];
    }

    public HashDigest(ReadOnlySpan<byte> bytes)
        : this(Bytes: [.. bytes])
    {
    }

    public ImmutableArray<byte> Bytes => _bytes.IsDefault ? DefaultByteArray : _bytes;

    public static HashDigest<T> Parse(string hex)
    {
        if (hex.Length != Size * 2)
        {
            var message = $"HashDigest<{typeof(T).Name}> requires {Size * 2} " +
                          $"hexadecimal letters, but {hex.Length} was given";
            throw new ArgumentOutOfRangeException(nameof(hex), message);
        }

        return new HashDigest<T>(ByteUtil.ParseHexToImmutable(hex));
    }

    public static HashDigest<T> DeriveFrom(byte[] input) => DeriveFrom(input.AsSpan());

    public static HashDigest<T> DeriveFrom(in ImmutableArray<byte> input)
        => DeriveFrom(input.AsSpan());

    public static HashDigest<T> DeriveFrom(ReadOnlySpan<byte> input)
    {
        Span<byte> buffer = stackalloc byte[Size];
        Algorithm.Value!.TryComputeHash(input, buffer, out _);
        return new HashDigest<T>([.. buffer]);
    }

    public override string ToString() => ToString("h", null);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var hex = ByteUtil.Hex(Bytes);
        return format switch
        {
            "h" => hex,
            "H" => hex.ToUpperInvariant(),
            null => hex,
            _ => throw new FormatException($"The format string '{format}' is not supported."),
        };
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

    public bool Equals(HashDigest<T> other)
    {
        for (var i = 0; i < Size; i++)
        {
            if (!Bytes[i].Equals(other.Bytes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<byte> ValidateBytes(in ImmutableArray<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            var message = $"HashDigest<{typeof(T).Name}> must be {Size} bytes, " +
                          $"but {bytes.Length} was given";
            throw new ArgumentOutOfRangeException(nameof(bytes), message);
        }

        return bytes;
    }
}
