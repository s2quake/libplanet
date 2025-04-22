using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bencodex;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;

namespace Libplanet.Types.Assets;

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

    [JsonInclude]
    public string Ticker { get; } = ValidateTicker(Ticker);

    [JsonInclude]
    public byte DecimalPlaces { get; } = DecimalPlaces;

    [JsonInclude]
    public BigInteger MaximumSupply { get; } = ValidateMaximumSupply(MaximumSupply);

    [JsonInclude]
    public ImmutableArray<Address> Minters { get; } = ValidateMinters(Minters);

    [JsonInclude]
    public bool IsTrackable { get; init; }

    [JsonIgnore]
    public HashDigest<SHA1> Hash => GetHash();

    public static FungibleAssetValue operator *(Currency currency, BigInteger quantity)
        => new(currency, majorUnit: quantity, minorUnit: 0);

    public static FungibleAssetValue operator *(BigInteger quantity, Currency currency)
        => new(currency, majorUnit: quantity, minorUnit: 0);

    public static Currency Create(IValue serialized)
    {
        if (serialized is not List list)
        {
            throw new ArgumentException("Serialized value must be a list.", nameof(serialized));
        }

        if (list.Count != 6)
        {
            throw new ArgumentException(
                "Serialized value must have exactly 6 elements.", nameof(serialized));
        }

        var ticker = ((Text)list[0]).Value;
        var decimalPlaces = (byte)((Integer)list[1]).Value;
        var maximumSupply = ((Integer)list[2]).Value;
        var minters = ((List)list[3]).Select(x => new Address(x)).ToImmutableArray();
        var isTrackable = ((Bencodex.Types.Boolean)list[5]).Value;
        return new Currency(ticker, decimalPlaces, maximumSupply, minters)
        {
            IsTrackable = isTrackable,
        };
    }

    public bool CanMint(Address address) => Minters.Length is 0 || Minters.Contains(address);

    public override string ToString() => $"{Ticker} ({Hash})";

    public override int GetHashCode() => Hash.GetHashCode();

    public bool Equals(Currency other) => Hash.Equals(other.Hash);

    public IValue ToBencodex()
    {
        return new List(
            new Text(Ticker),
            new Integer(DecimalPlaces),
            new Integer(MaximumSupply),
            ToBencodex(Minters),
            new Bencodex.Types.Boolean(IsTrackable));
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
                nameof(maximumSupply),
                "Maximum supply must be non-negative."
            );
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

    private static IValue ToBencodex(ImmutableArray<Address> minters)
    {
        if (minters.Length == 0)
        {
            return Null.Value;
        }

        return new List(minters.Select(item => item.Bencoded));
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
