using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.JsonConverters;

namespace Libplanet.Types;

[JsonConverter(typeof(CurrencyJsonConverter))]
[Model(Version = 1, TypeName = "Currency")]
public readonly partial record struct Currency
{
    public Currency()
    {
    }

    [Property(0)]
    [Required]
    [RegularExpression("^[A-Z]{3,5}$")]
    public required string Ticker { get; init; } = string.Empty;

    [Property(1)]
    public byte DecimalPlaces { get; init; }

    [Property(2)]
    [NonNegative]
    public BigInteger MaximumSupply { get; init; }

    [Property(3)]
    public ImmutableSortedSet<Address> Minters { get; init; } = [];

    public HashDigest<SHA1> Hash => HashDigest<SHA1>.HashData(ModelSerializer.SerializeToBytes(this));

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
        string ticker, byte decimalPlac2es, BigInteger maximumSupply, ImmutableSortedSet<Address> minters) => new()
        {
            Ticker = ticker,
            DecimalPlaces = decimalPlac2es,
            MaximumSupply = maximumSupply,
            Minters = minters,
        };

    public static Currency Create(string ticker, byte decimalPlaces) => new()
    {
        Ticker = ticker,
        DecimalPlaces = decimalPlaces,
    };

    public static Currency Create(string ticker, byte decimalPlaces, BigInteger maximumSupply) => new()
    {
        Ticker = ticker,
        DecimalPlaces = decimalPlaces,
        MaximumSupply = maximumSupply,
    };

    public static Currency Create(string ticker, byte decimalPlaces, ImmutableSortedSet<Address> minters) => new()
    {
        Ticker = ticker,
        DecimalPlaces = decimalPlaces,
        Minters = minters,
    };

    public bool CanMint(Address address) => Minters.Count is 0 || Minters.Contains(address);

    public BigInteger GetRawValue(BigInteger majorUnit, BigInteger minorUnit)
    {
        var factor = BigInteger.Pow(10, DecimalPlaces);
        if (BigInteger.Abs(minorUnit) >= factor)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minorUnit), $"Minor unit must be less than {10 ^ DecimalPlaces}.");
        }

        return (majorUnit * factor) + minorUnit;
    }

    public override string ToString() => $"{Ticker} ({Hash})";
}
