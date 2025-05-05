using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Assets;
using Libplanet.Types.Crypto;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Assets;

public class CurrencyTest
{
    public static readonly Address AddressA
        = Address.Parse("D6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9");

    public static readonly Address AddressB
        = Address.Parse("5003712B63baAB98094aD678EA2B24BcE445D076");

    [Fact]
    public void Constructor()
    {
        var foo = Currency.Create("FOO", 2);
        Assert.Equal("FOO", foo.Ticker);
        Assert.Equal(2, foo.DecimalPlaces);
        Assert.Equal(0, foo.MaximumSupply);
        Assert.Empty(foo.Minters);

        var bar = Currency.Create("BAR", 0, 100, [AddressA, AddressB]);
        Assert.Equal("BAR", bar.Ticker);
        Assert.Equal(0, bar.DecimalPlaces);
        Assert.Equal(100, bar.MaximumSupply);
        Assert.Equal<Address>(bar.Minters, [AddressB, AddressA]);

        var baz = Currency.Create("baz", 1, [AddressA]);
        Assert.Equal("baz", baz.Ticker);
        Assert.Equal(1, baz.DecimalPlaces);
        Assert.Equal(0, baz.MaximumSupply);
        Assert.Equal<Address>(baz.Minters, [AddressA]);

        var qux = Currency.Create("QUX", 0);
        Assert.Equal("QUX", qux.Ticker);
        Assert.Equal(0, qux.MaximumSupply);
        Assert.Equal(0, qux.DecimalPlaces);
        Assert.Empty(qux.Minters);

        var quux = Currency.Create("QUUX", 3, 100);
        Assert.Equal("QUUX", quux.Ticker);
        Assert.Equal(3, quux.DecimalPlaces);
        Assert.Equal(100, quux.MaximumSupply);
        Assert.Empty(quux.Minters);

        Assert.Throws<ValidationException>(
            () => TestValidator.Throws(Currency.Create(string.Empty, 0)));
        Assert.Throws<ValidationException>(
            () => TestValidator.Throws(Currency.Create("   \n", 1)));
        Assert.Throws<ValidationException>(
            () => TestValidator.Throws(Currency.Create("BAR", 1, [AddressA, AddressA])));
        Assert.Throws<ValidationException>(
            () => TestValidator.Throws(Currency.Create("TEST", 1, -100, [])));
    }

    [Fact]
    public void Hash()
    {
        var currency = Currency.Create("GOLD", 2, [AddressA]);
        var expected = HashDigest<SHA1>.Parse("1dbbf4c80fb861de779ec0543164021f9f157557");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("NCG", 8, [AddressA, AddressB]);
        expected = HashDigest<SHA1>.Parse("a612b8634d138931fec8f7ff7aaa37e5094650b2");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("FOO", 0);
        expected = HashDigest<SHA1>.Parse("f9136498a8bfe47631e762eb346393ba473839a3");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAR", 1);
        expected = HashDigest<SHA1>.Parse("21df4a6f399008f4fb7781d04e79d71d8102b78f");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAZ", 1);
        expected = HashDigest<SHA1>.Parse("8de3ddcc034949e569a548b6e5fdbcf988e56d04");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAZ", 1, 100);
        expected = HashDigest<SHA1>.Parse("85fe351431aa1e741215e883d2df7042e5dbb379");
        Assert.Equal(expected, currency.Hash);
    }

    [Fact]
    public void AllowsToMint()
    {
        var addressC = new PrivateKey().Address;
        var currency = Currency.Create("FOO", 0, [AddressA]);
        Assert.True(currency.CanMint(AddressA));
        Assert.False(currency.CanMint(AddressB));
        Assert.False(currency.CanMint(addressC));

        currency = Currency.Create("BAR", 2, [AddressA, AddressB]);
        Assert.True(currency.CanMint(AddressA));
        Assert.True(currency.CanMint(AddressB));
        Assert.False(currency.CanMint(addressC));

        currency = Currency.Create("BAZ", 0);
        Assert.True(currency.CanMint(AddressA));
        Assert.True(currency.CanMint(AddressB));
        Assert.True(currency.CanMint(addressC));

        currency = Currency.Create("QUX", 3, []);
        Assert.True(currency.CanMint(AddressA));
        Assert.True(currency.CanMint(AddressB));
        Assert.True(currency.CanMint(addressC));
    }

    [Fact]
    public void String()
    {
        var currency = Currency.Create("GOLD", 0, [AddressA]);
        Assert.Equal("GOLD (481a969169a78aa02c22082d4e8d05aba4dd07e6)", currency.ToString());

        currency = Currency.Create("GOLD", 0, []);
        Assert.Equal("GOLD (2e69027255d648cbc18d38caa33173c133a8caff)", currency.ToString());

        currency = Currency.Create("GOLD", 0);
        Assert.Equal("GOLD (2e69027255d648cbc18d38caa33173c133a8caff)", currency.ToString());

        currency = Currency.Create("GOLD", 0, 100, [AddressA]);
        Assert.Equal("GOLD (115a6236f9e14d4c6a19bdc054e9ce7ac667a194)", currency.ToString());
    }

    [Fact]
    public void Equal()
    {
        var currencyA = Currency.Create("GOLD", 0, [AddressA]);
        var currencyB = Currency.Create("GOLD", 0, [AddressA]);
        var currencyC = Currency.Create("GOLD", 1, [AddressA]);
        var currencyD = Currency.Create("GOLD", 0, [AddressB]);
        var currencyE = Currency.Create("SILVER", 2, [AddressA]);
        var currencyF = Currency.Create("GOLD", 0, [AddressA]);
        var currencyG = Currency.Create("GOLD", 0, 100, [AddressA]);
        var currencyH = Currency.Create("GOLD", 0, 200, [AddressA]);
        var currencyI = Currency.Create("SILVER", 0, 200, [AddressA]);

        Assert.Equal(currencyA, currencyA);
        Assert.Equal(currencyA, currencyB);
        Assert.NotEqual(currencyA, currencyC);
        Assert.NotEqual(currencyA, currencyD);
        Assert.NotEqual(currencyA, currencyE);
        Assert.Equal(currencyA, currencyF);
        Assert.NotEqual(currencyA, currencyG);
        Assert.NotEqual(currencyG, currencyH);
        Assert.NotEqual(currencyH, currencyI);
    }

    [Fact]
    public void GetFungibleAssetValue()
    {
        var foo = Currency.Create("FOO", 0);
        Assert.Equal(FungibleAssetValue.Create(foo, 123, 0), 123 * foo);
        Assert.Equal(FungibleAssetValue.Create(foo, -123, 0), foo * -123);

        var bar = Currency.Create("BAR", 2);
        Assert.Equal(FungibleAssetValue.Create(bar, 123, 1), 123.01m * bar);
    }

    [Fact]
    public void Serialize()
    {
        var foo = Currency.Create("FOO", 2);
        var bar = Currency.Create("BAR", 0, 100, [AddressA, AddressB]);

        Assert.Equal(foo, ModelSerializer.Deserialize<Currency>(ModelSerializer.Serialize(foo)));
        Assert.Equal(bar, ModelSerializer.Deserialize<Currency>(ModelSerializer.Serialize(bar)));
    }

    [SkippableFact]
    public void JsonSerialization()
    {
        var foo = Currency.Create("FOO", 2);
        AssertJsonSerializable(foo, @"
            {
                ""hash"": ""ea0ec4314a6124d97b42c8f9d15e961030c4f57b"",
                ""ticker"": ""FOO"",
                ""decimalPlaces"": 2,
                ""maximumSupply"": ""0"",
                ""minters"": []
            }
        ");

        var bar = Currency.Create("BAR", 0, 100, [AddressA, AddressB]);
        AssertJsonSerializable(bar, @"
            {
                ""hash"": ""07c0490bc1a5feb9567d4cf6e9672c775eb5dc48"",
                ""ticker"": ""BAR"",
                ""decimalPlaces"": 0,
                ""maximumSupply"": ""100"",
                ""minters"": [
                    ""0x5003712B63baAB98094aD678EA2B24BcE445D076"",
                    ""0xD6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9"",
                ]
            }
        ");
    }
}
