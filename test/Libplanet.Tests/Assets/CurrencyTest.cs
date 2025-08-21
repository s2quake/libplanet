using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types;
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

        ValidationTest.Throws(Currency.Create(string.Empty, 0), nameof(Currency.Ticker));
        ValidationTest.Throws(Currency.Create("   \n", 1), nameof(Currency.Ticker));
        ValidationTest.Throws(Currency.Create("bar", 1), nameof(Currency.Ticker));
        ValidationTest.Throws(Currency.Create("TEST", 1, -100, []), nameof(Currency.MaximumSupply));
    }

    [Fact]
    public void Hash()
    {
        var currency = Currency.Create("GOLD", 2, [AddressA]);
        var expected = HashDigest<SHA1>.Parse("c20c7a9bf9f39ba5b6dbf7f798bb4b6562e51e5e");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("NCG", 8, [AddressA, AddressB]);
        expected = HashDigest<SHA1>.Parse("976d50cc499205ad9e55085ecb93e7809f340814");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("FOO", 0);
        expected = HashDigest<SHA1>.Parse("9a6f39c23125e7b64ef3d16cfaac7243cf77f8f7");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAR", 1);
        expected = HashDigest<SHA1>.Parse("c10347fb5d6abc3ab92aa4f3d8d026bb50d5c6bc");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAZ", 1);
        expected = HashDigest<SHA1>.Parse("c5fda09873901efeb6d73104ac06bd7126141a53");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAZ", 1, 100);
        expected = HashDigest<SHA1>.Parse("92417bc9b80a82cf043c9752c7b37de3b9889372");
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
        Assert.Equal("GOLD (a563cb2ebd932576a033dbd6e0871064ee211ad2)", currency.ToString());

        currency = Currency.Create("GOLD", 0, []);
        Assert.Equal("GOLD (b392f57c9ec82c6f3c63fa504b2dc06196609c21)", currency.ToString());

        currency = Currency.Create("GOLD", 0);
        Assert.Equal("GOLD (b392f57c9ec82c6f3c63fa504b2dc06196609c21)", currency.ToString());

        currency = Currency.Create("GOLD", 0, 100, [AddressA]);
        Assert.Equal("GOLD (9cff87ba928f3f3c72aaa05cc24e1dc603ae18fd)", currency.ToString());
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

        Assert.Equal(foo, ModelSerializer.Clone(foo));
        Assert.Equal(bar, ModelSerializer.Clone(bar));
    }

    [Fact]
    public void JsonSerialization()
    {
        var foo = Currency.Create("FOO", 2);
        var expectedFooJson = @"
            {
                ""hash"": ""b48e64cd4355c04a1a07a921c2dc4e070ae274ff"",
                ""ticker"": ""FOO"",
                ""decimalPlaces"": 2,
                ""maximumSupply"": ""0"",
                ""minters"": []
            }";
        AssertJsonSerializable(foo, expectedFooJson);

        var bar = Currency.Create("BAR", 0, 100, [AddressA, AddressB]);
        var expectedBarJson = @"
            {
                ""hash"": ""6b187ae301440743c76edb7286a17d8982cac4eb"",
                ""ticker"": ""BAR"",
                ""decimalPlaces"": 0,
                ""maximumSupply"": ""100"",
                ""minters"": [
                    ""0x5003712B63baAB98094aD678EA2B24BcE445D076"",
                    ""0xD6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9"",
                ]
            }";
        AssertJsonSerializable(bar, expectedBarJson);
    }
}
