using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Bencodex.Types;
using Libplanet.Serialization;
using Libplanet.Types.JsonConverters;

namespace Libplanet.Types.Assets;

[JsonConverter(typeof(FungibleAssetValueJsonConverter))]
[Model(Version = 1)]
public readonly record struct FungibleAssetValue(
    [property: Property(0)] Currency Currency,
    [property: Property(1)] BigInteger RawValue)
    : IEquatable<FungibleAssetValue>, IComparable<FungibleAssetValue>, IComparable, IFormattable
{
    public FungibleAssetValue(Currency currency)
        : this(currency, BigInteger.Zero)
    {
    }

    public FungibleAssetValue(Currency currency, BigInteger majorUnit, BigInteger minorUnit)
        : this(currency, currency.GetRawValue(majorUnit, minorUnit))
    {
    }

    public bool IsPositive => RawValue > 0;

    public bool IsZero => RawValue == 0;

    public bool IsNegative => RawValue < 0;

    public int Sign => RawValue == 0 ? 0 : RawValue < 0 ? -1 : 1;

    public BigInteger MajorUnit => RawValue / BigInteger.Pow(10, Currency.DecimalPlaces);

    public BigInteger MinorUnit => RawValue % BigInteger.Pow(10, Currency.DecimalPlaces);

    public static bool operator <(FungibleAssetValue obj, FungibleAssetValue other)
        => obj.CompareTo(other) < 0;

    public static bool operator <=(FungibleAssetValue obj, FungibleAssetValue other)
        => obj.CompareTo(other) <= 0;

    public static bool operator >(FungibleAssetValue obj, FungibleAssetValue other)
        => other < obj;

    public static bool operator >=(FungibleAssetValue obj, FungibleAssetValue other)
        => other <= obj;

    public static FungibleAssetValue operator -(FungibleAssetValue value)
        => new(value.Currency, -value.RawValue);

    public static FungibleAssetValue operator +(FungibleAssetValue left, FungibleAssetValue right)
    {
        if (!left.Currency.Equals(right.Currency))
        {
            var message = $"Unable to add heterogeneous currencies: " +
                          $"{left.Currency} \u2260 {right.Currency}.";
            throw new ArgumentException(message, nameof(right));
        }

        return new FungibleAssetValue(left.Currency, left.RawValue + right.RawValue);
    }

    public static FungibleAssetValue operator -(FungibleAssetValue left, FungibleAssetValue right)
    {
        if (!left.Currency.Equals(right.Currency))
        {
            var message = $"Unable to subtract heterogeneous currencies: " +
                          $"{left.Currency} \u2260 {right.Currency}.";
            throw new ArgumentException(message, nameof(right));
        }

        return new FungibleAssetValue(left.Currency, left.RawValue - right.RawValue);
    }

    public static FungibleAssetValue operator *(FungibleAssetValue left, BigInteger right)
        => new(left.Currency, left.RawValue * right);

    public static FungibleAssetValue operator *(BigInteger left, FungibleAssetValue right)
        => new(right.Currency, left * right.RawValue);

    public static FungibleAssetValue operator %(FungibleAssetValue dividend, BigInteger divisor)
        => new(dividend.Currency, dividend.RawValue % divisor);

    public static FungibleAssetValue operator %(
        FungibleAssetValue dividend, FungibleAssetValue divisor)
    {
        if (!dividend.Currency.Equals(divisor.Currency))
        {
            var message = $"Cannot be divided by a heterogeneous currency: " +
                          $"{dividend.Currency} \u2260 {divisor.Currency}.";
            throw new ArgumentException(message, nameof(divisor));
        }

        return new FungibleAssetValue(dividend.Currency, dividend.RawValue % divisor.RawValue);
    }

    public static FungibleAssetValue Create(IValue value)
    {
        if (value is not List list)
        {
            throw new ArgumentException($"The given value is not a list: {value}", nameof(value));
        }

        var currency = Currency.Create(list[0]);
        var rawValue = (Integer)list[1];
        return new FungibleAssetValue(currency, rawValue);
    }

    public static FungibleAssetValue Parse(Currency currency, string value)
    {
        var decimalPlaces = currency.DecimalPlaces;
        var pattern = $"^(?<sign>[+-]?)(?<major>[0-9]+)(\\.(?<minor>[0-9]{{0,{decimalPlaces}}}))?$";
        var match = Regex.Match(value, pattern);
        if (!match.Success)
        {
            throw new FormatException(
                "The value string must consist of digits, decimal separator (.), plus (+), " +
                "and minus(-).");
        }

        var sign = match.Groups["sign"].Value;
        var isPositive = sign != "-";
        var majorString = match.Groups["major"].Value.PadLeft(1, '0');
        var minorString = match.Groups["minor"].Value.PadRight(decimalPlaces, '0');
        var major = BigInteger.Parse(majorString);
        var minor = BigInteger.Parse(minorString);
        var rawValue = major * BigInteger.Pow(10, currency.DecimalPlaces) + minor;
        if (!isPositive)
        {
            rawValue = -rawValue;
        }

        return new FungibleAssetValue(currency, rawValue);
    }

    public static FungibleAssetValue Abs(FungibleAssetValue value)
        => new(value.Currency, BigInteger.Abs(value.RawValue));

    public FungibleAssetValue DivRem(BigInteger divisor, out FungibleAssetValue remainder)
    {
        var value = BigInteger.DivRem(RawValue, divisor, out BigInteger rem);
        remainder = new FungibleAssetValue(Currency, rem);
        return new FungibleAssetValue(Currency, value);
    }

    public BigInteger DivRem(FungibleAssetValue divisor, out FungibleAssetValue remainder)
    {
        if (!Currency.Equals(divisor.Currency))
        {
            var message = $"Cannot be divided by a heterogeneous currency: " +
                          $"{Currency} \u2260 {divisor.Currency}.";
            throw new ArgumentException(message, nameof(divisor));
        }

        var value = BigInteger.DivRem(RawValue, divisor.RawValue, out var rem);
        remainder = new FungibleAssetValue(Currency, rem);
        return value;
    }

    public (FungibleAssetValue Quotient, FungibleAssetValue Remainder) DivRem(BigInteger divisor)
        => (DivRem(divisor, out FungibleAssetValue remainder), remainder);

    public (BigInteger Quotient, FungibleAssetValue Remainder) DivRem(FungibleAssetValue divisor)
        => (DivRem(divisor, out FungibleAssetValue remainder), remainder);

    public string GetQuantityString() => GetQuantityString(false);

    public string GetQuantityString(bool fixedDecimalPlaces)
    {
        var sign = IsNegative ? "-" : string.Empty;
        var decimalPlaces = Currency.DecimalPlaces;
        var rawString = $"{BigInteger.Abs(RawValue)}".PadLeft(decimalPlaces + 1, '0');
        rawString = rawString.Insert(rawString.Length - decimalPlaces, ".");
        if (!fixedDecimalPlaces)
        {
            rawString = rawString.TrimEnd('0').TrimEnd('.');
        }

        return $"{sign}{rawString}";
    }

    public bool Equals(FungibleAssetValue? other)
        => other is { } fav && Currency.Equals(fav.Currency) && RawValue.Equals(fav.RawValue);

    public override int GetHashCode()
        => unchecked((Currency.GetHashCode() * 397) ^ RawValue.GetHashCode());

    public int CompareTo(object? obj)
    {
        if (obj is not FungibleAssetValue o)
        {
            throw new ArgumentException(
                $"Unable to compare with other than {nameof(FungibleAssetValue)}",
                nameof(obj));
        }

        return CompareTo(o);
    }

    public int CompareTo(FungibleAssetValue other)
    {
        if (!Currency.Equals(other.Currency))
        {
            throw new ArgumentException(
                $"Unable to compare heterogeneous currencies: {Currency} \u2260 {other.Currency}.",
                nameof(other));
        }

        return RawValue.CompareTo(other.RawValue);
    }

    public override string ToString() => $"{GetQuantityString()} {Currency.Ticker}";

    public string ToString(string? format, IFormatProvider? formatProvider) => format switch
    {
        "N" => GetQuantityString(),
        "F" => GetQuantityString(fixedDecimalPlaces: true),
        "C" => $"{GetQuantityString()} {Currency.Ticker}",
        "G" => $"{GetQuantityString(fixedDecimalPlaces: true)} {Currency.Ticker}",
        _ => ToString(),
    };

    public IValue ToBencodex()
    {
        return new List(
            Currency.ToBencodex(),
            (Integer)RawValue);
    }
}
