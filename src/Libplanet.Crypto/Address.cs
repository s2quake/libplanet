using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto.Converters;
using Libplanet.Crypto.JsonConverters;

namespace Libplanet.Crypto;

[TypeConverter(typeof(AddressTypeConverter))]
[JsonConverter(typeof(AddressJsonConverter))]
public readonly record struct Address(in ImmutableArray<byte> ByteArray)
    : IEquatable<Address>, IComparable<Address>, IComparable, IFormattable
{
    public const int Size = 20;

    private static readonly ImmutableArray<byte> _defaultByteArray
        = ImmutableArray.Create(new byte[Size]);

    private readonly ImmutableArray<byte> _bytes = ValidateBytes(ByteArray);

    public Address(ReadOnlySpan<byte> bytes)
        : this(ValidateBytes(bytes.ToImmutableArray()))
    {
    }

    public Address(PublicKey publicKey)
        : this(DeriveAddress(publicKey))
    {
    }

    public ImmutableArray<byte> ByteArray => _bytes.IsDefault ? _defaultByteArray : _bytes;

    public IValue ToBencodex() => new Binary(ByteArray);

    public static Address Parse(string hex) => new(DeriveAddress(hex));

    public static Address Create(IValue bencoded) => new Address(GetBytes(bencoded));

    public bool Equals(Address other) => ByteArray.SequenceEqual(other.ByteArray);

    public override int GetHashCode() => ByteUtil.CalculateHashCode(ToByteArray());

    public byte[] ToByteArray() => [.. ByteArray];

    public override string ToString() => $"0x{ToChecksumAddress(ByteUtil.Hex(ToByteArray()))}";

    public string ToString(string? format, IFormatProvider? formatProvider) => format switch
    {
        "raw" => ToChecksumAddress(ByteUtil.Hex(ToByteArray())),
        _ => ToString(),
    };

    public int CompareTo(Address other)
    {
        var self = ByteArray;
        var operand = other.ByteArray;

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

    public int CompareTo(object? obj) => obj is Address other
        ? CompareTo(other)
        : throw new ArgumentException(
            $"Argument {nameof(obj)} is not an ${nameof(Address)}.", nameof(obj));

    private static ImmutableArray<byte> ValidateBytes(in ImmutableArray<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException(
                $"Given {nameof(bytes)} must be 20 bytes", nameof(bytes));
        }

        return bytes;
    }

    private static ImmutableArray<byte> GetBytes(IValue value)
    {
        if (value is Binary binary)
        {
            return binary.ByteArray;
        }

        throw new ArgumentException(
            $"Given {nameof(value)} must be a {nameof(Binary)}: {value.GetType()}",
            nameof(value));
    }

    private static string ToChecksumAddress(string hex)
    {
        var value = new Nethereum.Util.AddressUtil().ConvertToChecksumAddress(hex);
        if (value.StartsWith("0x"))
        {
            return value[2..];
        }

        return value;
    }

    private static ImmutableArray<byte> DeriveAddress(PublicKey key)
    {
        var initaddr = new Nethereum.Util.Sha3Keccack().CalculateHash(
            GetPubKeyNoPrefix(key, false));
        var addr = new byte[initaddr.Length - 12];
        Array.Copy(initaddr, 12, addr, 0, initaddr.Length - 12);
        var address = ToChecksumAddress(
            Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(addr));
        return ByteUtil.ParseHexToImmutable(address);
    }

    private static byte[] GetPubKeyNoPrefix(PublicKey publicKey, bool compressed = false)
    {
        var pubKey = publicKey.ToByteArray(compressed);
        var arr = new byte[pubKey.Length - 1];
        Array.Copy(pubKey, 1, arr, 0, arr.Length);
        return arr;
    }

    private static ImmutableArray<byte> DeriveAddress(string hex)
    {
        if (hex.Length != 40 && hex.Length != 42)
        {
            throw new ArgumentException(
                $"Address hex must be either 42 chars or 40 chars, " +
                $"but given {nameof(hex)} is of length {hex.Length}: {hex}",
                nameof(hex));
        }

        if (hex.Length == 42)
        {
            if (!hex.StartsWith("0x"))
            {
                throw new ArgumentException(
                    $"Address hex of length 42 chars must start with \"0x\" prefix: {hex}",
                    nameof(hex));
            }

            hex = hex[2..];
        }

        if (hex.ToLower(CultureInfo.InvariantCulture) != hex &&
            ToChecksumAddress(hex.ToLower(CultureInfo.InvariantCulture)) != hex)
        {
            throw new ArgumentException("Address checksum is invalid", nameof(hex));
        }

        try
        {
            return ByteUtil.ParseHexToImmutable(hex);
        }
        catch (FormatException e)
        {
            throw new ArgumentException(
                "Address hex must only consist of ASCII characters", nameof(hex), e);
        }
    }
}
