using System.Security.Cryptography;
using Libplanet.Serialization;
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

        ValidationUtility.Throws(Currency.Create(string.Empty, 0), nameof(Currency.Ticker));
        ValidationUtility.Throws(Currency.Create("   \n", 1), nameof(Currency.Ticker));
        ValidationUtility.Throws(Currency.Create("bar", 1), nameof(Currency.Ticker));
        ValidationUtility.Throws(Currency.Create("TEST", 1, -100, []), nameof(Currency.MaximumSupply));
    }

    [Fact]
    public void Hash()
    {
        var currency = Currency.Create("GOLD", 2, [AddressA]);
        var expected = HashDigest<SHA1>.Parse("bc392fe514f5bce051a25229a2d5404d24ffe002");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("NCG", 8, [AddressA, AddressB]);
        expected = HashDigest<SHA1>.Parse("3a7151e1d9ea503b19b6e48c206f35b53a029e9a");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("FOO", 0);
        expected = HashDigest<SHA1>.Parse("6f98bda5dd03c801b331603dfd004c1b1cca818b");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAR", 1);
        expected = HashDigest<SHA1>.Parse("a10d99ab4936f60f506f95a50d660ef6b2abb4f1");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAZ", 1);
        expected = HashDigest<SHA1>.Parse("fb0b364837999b1eb88a397d2c42900e0cbae26c");
        Assert.Equal(expected, currency.Hash);

        currency = Currency.Create("BAZ", 1, 100);
        expected = HashDigest<SHA1>.Parse("98d5d0dfb130e8ba45109e480e4556fc56173e1f");
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
        Assert.Equal("GOLD (c6803ba4c2f582e5038f3698a00299b5cea63441)", currency.ToString());

        currency = Currency.Create("GOLD", 0, []);
        Assert.Equal("GOLD (dbd5a3f3f0c7b6dfa008854ae3e910494aca28d6)", currency.ToString());

        currency = Currency.Create("GOLD", 0);
        Assert.Equal("GOLD (dbd5a3f3f0c7b6dfa008854ae3e910494aca28d6)", currency.ToString());

        currency = Currency.Create("GOLD", 0, 100, [AddressA]);
        Assert.Equal("GOLD (f747f97271791fac1ea068befeff80b7e0727b19)", currency.ToString());
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
                ""hash"": ""8079efca64fc19121f21f5e04d5d9a303c96adc5"",
                ""ticker"": ""FOO"",
                ""decimalPlaces"": 2,
                ""maximumSupply"": ""0"",
                ""minters"": []
            }";
        AssertJsonSerializable(foo, expectedFooJson);

        var bar = Currency.Create("BAR", 0, 100, [AddressA, AddressB]);
        var expectedBarJson = @"
            {
                ""hash"": ""9bc0d04176336a20eef24cc975f2e4524f303ec1"",
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
