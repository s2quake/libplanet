using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.JsonConverters;

namespace Libplanet.Types.Assets;

[JsonConverter(typeof(CurrencyJsonConverter))]
[Model(Version = 1)]
public readonly record struct Currency(
    string Ticker, byte DecimalPlaces, BigInteger MaximumSupply, ImmutableArray<Address> Minters)
    : IEquatable<Currency>
{
    public Currency(string ticker, byte decimalPlaces)
        : this(ticker, decimalPlaces, 0, [])
    {
    }

    public Currency(string ticker, byte decimalPlaces, BigInteger maximumSupply)
        : this(ticker, decimalPlaces, maximumSupply, [])
    {
    }

    public Currency(string ticker, byte decimalPlaces, ImmutableArray<Address> minters)
        : this(ticker, decimalPlaces, 0, minters)
    {
    }

    [Property(0)]
    public string Ticker { get; } = ValidateTicker(Ticker);

    [Property(1)]
    public byte DecimalPlaces { get; } = DecimalPlaces;

    [Property(2)]
    public BigInteger MaximumSupply { get; } = ValidateMaximumSupply(MaximumSupply);

    [Property(3)]
    public ImmutableArray<Address> Minters { get; } = ValidateMinters(Minters);

    public HashDigest<SHA1> Hash => GetHash();

    public static FungibleAssetValue operator *(Currency currency, decimal value)
    {
        var major = Math.Floor(value);
        var decimalPlaces = currency.DecimalPlaces;
        var fractionalPart = value - major;
        var minor = fractionalPart * (decimal)BigInteger.Pow(10, decimalPlaces);
        var rawValue = currency.GetRawValue((BigInteger)major, (BigInteger)minor);
        return new FungibleAssetValue(currency, rawValue);
    }

    public static FungibleAssetValue operator *(decimal value, Currency currency)
        => currency * value;

    public static Currency Create(IValue serialized)
    {
        if (serialized is not List list)
        {
            throw new ArgumentException("Serialized value must be a list.", nameof(serialized));
        }

        if (list.Count != 4)
        {
            throw new ArgumentException(
                "Serialized value must have exactly 6 elements.", nameof(serialized));
        }

        var ticker = ((Text)list[0]).Value;
        var decimalPlaces = (byte)((Integer)list[1]).Value;
        var maximumSupply = ((Integer)list[2]).Value;
        var minters = FromBencodex((List)list[3]);
        return new Currency(ticker, decimalPlaces, maximumSupply, minters);
    }

    public bool CanMint(Address address) => Minters.Length is 0 || Minters.Contains(address);

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

    public IValue ToBencodex()
    {
        return new List(
            new Text(Ticker),
            new Integer(DecimalPlaces),
            new Integer(MaximumSupply),
            ToBencodex(Minters));
    }

    private static string ValidateTicker(string ticker)
    {
        if (ticker == string.Empty)
        {
            throw new ArgumentException(
                "Currency ticker symbol cannot be empty.",
                nameof(ticker)
            );
        }

        if (ticker.Trim() != ticker)
        {
            throw new ArgumentException(
                "Currency ticker symbol cannot have leading or trailing spaces.",
                nameof(ticker)
            );
        }

        return ticker;
    }

    private static BigInteger ValidateMaximumSupply(BigInteger maximumSupply)
    {
        if (maximumSupply < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSupply), "Maximum supply must be non-negative.");
        }

        return maximumSupply;
    }

    private static ImmutableArray<Address> ValidateMinters(ImmutableArray<Address> minters)
    {
        if (minters.IsDefaultOrEmpty)
        {
            return [];
        }

        if (minters.Distinct().Count() != minters.Length)
        {
            throw new ArgumentException("Minters must be unique.", nameof(minters));
        }

        return [.. minters.Order()];
    }

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
        var codec = new Codec();
        codec.Encode(ToBencodex(), stream);
        stream.FlushFinalBlock();
        if (sha1.Hash is { } hash)
        {
            return new HashDigest<SHA1>(sha1.Hash);
        }

        throw new InvalidOperationException("Failed to compute the hash.");
    }
}
