using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using Libplanet.Serialization;
using Libplanet.Types.Converters;
using Libplanet.Types.Crypto;
using Libplanet.Types.JsonConverters;
using Libplanet.Types.ModelConverters;

namespace Libplanet.Types;

[TypeConverter(typeof(AddressTypeConverter))]
[JsonConverter(typeof(AddressJsonConverter))]
[ModelConverter(typeof(AddressModelConverter))]
public readonly partial record struct Address(in ImmutableArray<byte> Bytes)
    : IEquatable<Address>, IComparable<Address>, IComparable, IFormattable
{
    public const int Size = 20;

    private static readonly ImmutableArray<byte> _defaultByteArray
        = ImmutableArray.Create(new byte[Size]);

    private readonly ImmutableArray<byte> _bytes = ValidateBytes(Bytes);

    public Address(ReadOnlySpan<byte> bytes)
        : this(ValidateBytes(bytes.ToImmutableArray()))
    {
    }

    public Address(PublicKey publicKey)
        : this(DeriveAddress(publicKey))
    {
    }

    public ImmutableArray<byte> Bytes => _bytes.IsDefault ? _defaultByteArray : _bytes;

    public static Address Parse(string hex)
    {
        try
        {
            return new(DeriveAddress(hex));
        }
        catch (Exception e)
        {
            throw new FormatException($"Failed to parse {nameof(Address)} from hex string: {hex}", e);
        }
    }

    public bool Verify(ImmutableArray<byte> message, ImmutableArray<byte> signature)
    {
        try
        {
            return CryptoConfig.CryptoBackend.Verify([.. message], [.. signature], this);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool Equals(Address other) => Bytes.SequenceEqual(other.Bytes);

    public override int GetHashCode() => ByteUtility.CalculateHashCode(ToByteArray());

    public byte[] ToByteArray() => [.. Bytes];

    public override string ToString() => $"0x{ToChecksumAddress(ByteUtility.Hex(ToByteArray()))}";

    public string ToString(string? format, IFormatProvider? formatProvider) => format switch
    {
        "raw" => ToChecksumAddress(ByteUtility.Hex(ToByteArray())),
        _ => ToString(),
    };

    public int CompareTo(Address other)
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
        Address other => CompareTo(other),
        _ => throw new ArgumentException($"Argument {nameof(obj)} is not ${nameof(Address)}.", nameof(obj)),
    };

    private static ImmutableArray<byte> ValidateBytes(in ImmutableArray<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException(
                $"Given {nameof(bytes)} must be 20 bytes", nameof(bytes));
        }

        return bytes;
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

    private static byte[] GetPubKeyNoPrefix(PublicKey publicKey, bool compressed = false)
    {
        var pubKey = publicKey.ToByteArray(compressed);
        var bytes = new byte[pubKey.Length - 1];
        Array.Copy(pubKey, 1, bytes, 0, bytes.Length);
        return bytes;
    }

    private static ImmutableArray<byte> DeriveAddress(PublicKey publicKey)
    {
        var initaddr = new Nethereum.Util.Sha3Keccack().CalculateHash(
            GetPubKeyNoPrefix(publicKey, false));
        var bytes = new byte[initaddr.Length - 12];
        Array.Copy(initaddr, 12, bytes, 0, initaddr.Length - 12);
        var address = ToChecksumAddress(
            Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(bytes));
        return ByteUtility.ParseHexToImmutable(address);
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
            return ByteUtility.ParseHexToImmutable(hex);
        }
        catch (FormatException e)
        {
            throw new ArgumentException(
                "Address hex must only consist of ASCII characters", nameof(hex), e);
        }
    }
}
