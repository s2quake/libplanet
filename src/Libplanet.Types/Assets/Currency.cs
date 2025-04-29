using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.JsonConverters;

namespace Libplanet.Types.Assets;

[JsonConverter(typeof(CurrencyJsonConverter))]
[Model(Version = 1)]
public readonly record struct Currency : IEquatable<Currency>
{
    public Currency()
    {
    }

    [Property(0)]
    [RegularExpression("^[A-Z]{3,5}$")]
    public required string Ticker { get; init; } = string.Empty;

    [Property(1)]
    public byte DecimalPlaces { get; init; }

    [Property(2)]
    [NonNegative]
    public BigInteger MaximumSupply { get; init; }

    [Property(3)]
    [NonDefault]
    public ImmutableSortedSet<Address> Minters { get; init; } = [];

    public HashDigest<SHA1> Hash => GetHash();

    public static FungibleAssetValue operator *(Currency currency, decimal value)
    {
        var major = Math.Floor(value);
        var decimalPlaces = currency.DecimalPlaces;
        var fractionalPart = value - major;
        var minor = fractionalPart * (decimal)BigInteger.Pow(10, decimalPlaces);
        var rawValue = currency.GetRawValue((BigInteger)major, (BigInteger)minor);
        return new FungibleAssetValue { Currency = currency, RawValue = rawValue };
    }

    public static FungibleAssetValue operator *(decimal value, Currency currency) => currency * value;

    public static Currency Create(
        string ticker, byte decimalPlac2es, BigInteger maximumSupply, ImmutableSortedSet<Address> minters)
    {
        return new Currency
        {
            Ticker = ticker,
            DecimalPlaces = decimalPlac2es,
            MaximumSupply = maximumSupply,
            Minters = minters,
        };
    }

    public static Currency Create(string ticker, byte decimalPlaces)
    {
        return new Currency
        {
            Ticker = ticker,
            DecimalPlaces = decimalPlaces,
        };
    }

    public static Currency Create(string ticker, byte decimalPlaces, BigInteger maximumSupply)
    {
        return new Currency
        {
            Ticker = ticker,
            DecimalPlaces = decimalPlaces,
            MaximumSupply = maximumSupply,
        };
    }

    public static Currency Create(string ticker, byte decimalPlaces, ImmutableSortedSet<Address> minters)
    {
        return new Currency
        {
            Ticker = ticker,
            DecimalPlaces = decimalPlaces,
            Minters = minters,
        };
    }

    public bool CanMint(Address address) => Minters.Count is 0 || Minters.Contains(address);

    public BigInteger GetRawValue(BigInteger majorUnit, BigInteger minorUnit)
    {
        var factor = BigInteger.Pow(10, DecimalPlaces);
        if (BigInteger.Abs(minorUnit) >= factor)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minorUnit), $"Minor unit must be less than {10 ^ DecimalPlaces}.");
        }

        return majorUnit * factor + minorUnit;
    }

    public override string ToString() => $"{Ticker} ({Hash})";

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public bool Equals(Currency? other) => ModelUtility.Equals(this, other);

    private static SHA1 GetSHA1()
    {
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1
        try
        {
            return new SHA1CryptoServiceProvider();
        }
        catch (PlatformNotSupportedException)
        {
            return new SHA1Managed();
        }
#elif NET6_0_OR_GREATER
        return SHA1.Create();
#endif
    }

    private static List ToBencodex(ImmutableArray<Address> minters)
    {
        if (minters.Length == 0)
        {
            return List.Empty;
        }

        var list = new List(minters.Select(item => ModelSerializer.Serialize(item)));
        return list;
    }

    private static ImmutableArray<Address> FromBencodex(List list)
    {
        return [.. list.Select(ModelSerializer.Deserialize<Address>)];
    }

    private HashDigest<SHA1> GetHash()
    {
        using var buffer = new MemoryStream();
        using var sha1 = GetSHA1();
        using var stream = new CryptoStream(buffer, sha1, CryptoStreamMode.Write);
        buffer.Write(ModelSerializer.SerializeToBytes(this));
        stream.FlushFinalBlock();
        if (sha1.Hash is { } hash)
        {
            return new HashDigest<SHA1>(sha1.Hash);
        }

        throw new InvalidOperationException("Failed to compute the hash.");
    }
}
