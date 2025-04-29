using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Assets;
using Xunit;
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
            () => TestValidator.Validate(Currency.Create(string.Empty, 0)));
        Assert.Throws<ValidationException>(
            () => TestValidator.Validate(Currency.Create("   \n", 1)));
        Assert.Throws<ValidationException>(
            () => TestValidator.Validate(Currency.Create("BAR", 1, [AddressA, AddressA])));
        Assert.Throws<ValidationException>(
            () => TestValidator.Validate(Currency.Create("TEST", 1, -100, [])));
    }

    [Fact]
    public void Hash()
    {
        var currency = Currency.Create("GOLD", 2, [AddressA]);
        var expected = HashDigest<SHA1>.Parse("391917d64815d57a0551ffeca31c35bc43a88c47");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("NCG", 8, [AddressA, AddressB]);
        expected = HashDigest<SHA1>.Parse("42bae64d960ea87cb3a76d158b9f2ecd6ecdba72");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("FOO", 0);
        expected = HashDigest<SHA1>.Parse("1ce777fc889944e41600c3aa69a999f982887520");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAR", 1);
        expected = HashDigest<SHA1>.Parse("ce1fa48db5f003dc8566da6bea0e7ddcf2659a27");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAZ", 1);
        expected = HashDigest<SHA1>.Parse("8538939433c24315bc751076845e0d4ab31c4ba5");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAZ", 1, 100);
        expected = HashDigest<SHA1>.Parse("2e94d996f738afca6e9a554345f6f2dc2b2f639c");
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
