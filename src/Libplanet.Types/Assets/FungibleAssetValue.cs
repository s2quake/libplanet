using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Types.JsonConverters;

namespace Libplanet.Types.Assets;

[JsonConverter(typeof(FungibleAssetValueJsonConverter))]
public readonly record struct FungibleAssetValue(Currency Currency, BigInteger RawValue)
    : IEquatable<FungibleAssetValue>, IComparable<FungibleAssetValue>, IComparable
{
    public FungibleAssetValue(Currency currency)
        : this(currency, BigInteger.Zero)
    {
    }

    public FungibleAssetValue(Currency currency, BigInteger majorUnit, BigInteger minorUnit)
        : this(
            currency,
            majorUnit.IsZero ? minorUnit.Sign : majorUnit.Sign,
            BigInteger.Abs(majorUnit),
            BigInteger.Abs(minorUnit)
        )
    {
        if (!majorUnit.IsZero && minorUnit < BigInteger.Zero)
        {
            throw new ArgumentException(
                "Unless the major unit is zero, the minor unit cannot be negative.",
                nameof(minorUnit));
        }
    }

    public FungibleAssetValue(
        Currency currency, int sign, BigInteger majorUnit, BigInteger minorUnit)
        : this(
            currency,
            sign * (majorUnit * BigInteger.Pow(10, currency.DecimalPlaces) + minorUnit)
        )
    {
        if (sign > 1 || sign < -1)
        {
            throw new ArgumentException("The sign must be 1, 0, or -1.", nameof(sign));
        }
        else if (sign == 0 && !majorUnit.IsZero)
        {
            throw new ArgumentException(
                "If the sign is zero, the major unit must be also zero.",
                nameof(majorUnit)
            );
        }
        else if (sign == 0 && !minorUnit.IsZero)
        {
            throw new ArgumentException(
                "If the sign is zero, the minor unit must be also zero.",
                nameof(minorUnit)
            );
        }
        else if (majorUnit < 0)
        {
            throw new ArgumentException(
                "The major unit must not be negative.",
                nameof(majorUnit)
            );
        }
        else if (minorUnit < 0)
        {
            throw new ArgumentException(
                "The minor unit must not be negative.",
                nameof(minorUnit)
            );
        }
        else if (minorUnit > 0 &&
                 (int)Math.Floor(BigInteger.Log10(minorUnit) + 1) > currency.DecimalPlaces)
        {
            string msg =
                $"Since the currency {currency} allows upto {currency.DecimalPlaces} " +
                $"decimal places, the given minor unit {minorUnit} is too big.";
            throw new ArgumentException(msg, nameof(minorUnit));
        }
    }

    public int Sign
    {
        get
        {
            if (RawValue < 0)
            {
                return -1;
            }

            if (RawValue == 0)
            {
                return 0;
            }

            return 1;
        }
    }

    public BigInteger MajorUnit
        => BigInteger.Abs(RawValue) / BigInteger.Pow(10, Currency.DecimalPlaces);

    public BigInteger MinorUnit
        => BigInteger.Abs(RawValue) % BigInteger.Pow(10, Currency.DecimalPlaces);

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
        int sign = 1;
        if (value[0] == '-' || value[0] == '+')
        {
            sign = value[0] == '-' ? -1 : 1;
            value = value[1..];
        }

        if (value.IndexOfAny(['+', '-']) >= 0)
        {
            var message = "Plus (+) or minus (-) sign can be appeared only at first and " +
                          "cannot be more than one.";
            throw new FormatException(message);
        }

        string[] parts = value.Split(['.'], count: 2);
        bool minorExist = parts.Length > 1;
        if (minorExist && parts[1].IndexOf('.') >= 0)
        {
            throw new FormatException(
                "The decimal separator (.) cannot be appeared more than once."
            );
        }
        else if (!parts[0].All(char.IsDigit) || (minorExist && !parts[1].All(char.IsDigit)))
        {
            const string msg =
                "The value string must consist of digits, decimal separator (.), plus (+), " +
                "and minus(-).";
            throw new FormatException(msg);
        }
        else if (minorExist && parts[1].Length > currency.DecimalPlaces)
        {
            throw new FormatException(
                $"The currency {currency} does not allow more than {currency.DecimalPlaces} " +
                (currency.DecimalPlaces == 1 ? "decimal place" : "decimal places")
            );
        }

        BigInteger major = BigInteger.Parse(parts[0], CultureInfo.InvariantCulture);
        BigInteger minor = minorExist
            ? BigInteger.Parse(parts[1], CultureInfo.InvariantCulture) * BigInteger.Pow(
                10,
                currency.DecimalPlaces - parts[1].Length)
            : 0;
        return new FungibleAssetValue(currency, sign, major, minor);
    }

    public FungibleAssetValue DivRem(BigInteger divisor, out FungibleAssetValue remainder)
    {
        BigInteger q = BigInteger.DivRem(RawValue, divisor, out BigInteger rem);
        remainder = new FungibleAssetValue(Currency, rem);
        return new FungibleAssetValue(Currency, q);
    }

    public BigInteger DivRem(FungibleAssetValue divisor, out FungibleAssetValue remainder)
    {
        if (!Currency.Equals(divisor.Currency))
        {
            throw new ArgumentException(
                "Cannot be divided by a heterogeneous currency: " +
                $"{Currency} \u2260 {divisor.Currency}."
            );
        }

        BigInteger d = BigInteger.DivRem(RawValue, divisor.RawValue, out BigInteger rem);
        remainder = new FungibleAssetValue(Currency, rem);
        return d;
    }

    public (FungibleAssetValue Quotient, FungibleAssetValue Remainder) DivRem(BigInteger divisor)
        => (DivRem(divisor, out FungibleAssetValue remainder), remainder);

    public (BigInteger Quotient, FungibleAssetValue Remainder) DivRem(FungibleAssetValue divisor)
        => (DivRem(divisor, out FungibleAssetValue remainder), remainder);

    public FungibleAssetValue Abs() => new(Currency, BigInteger.Abs(RawValue));

    public string GetQuantityString(bool minorUnit = false)
    {
        var signedString = Sign < 0 ? "-" : string.Empty;
        var endCharsToTrim = minorUnit ? ' ' : '0';
        return minorUnit || MinorUnit > 0
            ? string.Format(
            CultureInfo.InvariantCulture,
            "{0}{1}.{2:d" + Currency.DecimalPlaces.ToString(CultureInfo.InvariantCulture) + "}",
            signedString,
            MajorUnit,
            MinorUnit
            ).TrimEnd(endCharsToTrim)
            : (MajorUnit * Sign).ToString(CultureInfo.InvariantCulture);
    }

    public bool Equals(FungibleAssetValue other)
        => Currency.Equals(other.Currency) && RawValue.Equals(other.RawValue);

    public override int GetHashCode()
        => unchecked((Currency.GetHashCode() * 397) ^ RawValue.GetHashCode());

    public int CompareTo(object? obj) => obj is FungibleAssetValue o
        ? CompareTo(o)
        : throw new ArgumentException(
            $"Unable to compare with other than {nameof(FungibleAssetValue)}",
            nameof(obj));

    public int CompareTo(FungibleAssetValue other) => Currency.Equals(other.Currency)
        ? RawValue.CompareTo(other.RawValue)
        : throw new ArgumentException(
            $"Unable to compare heterogeneous currencies: {Currency} \u2260 {other.Currency}.",
            nameof(other));

    public override string ToString()
        => $"{GetQuantityString()} {Currency.Ticker}";

    public IValue ToBencodex()
    {
        return new List(
            Currency.ToBencodex(),
            (Integer)RawValue);
    }
}
