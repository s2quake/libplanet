using System;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
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
        var foo = new Currency("FOO", 2);
        Assert.Equal("FOO", foo.Ticker);
        Assert.Equal(2, foo.DecimalPlaces);
        Assert.Equal(0, foo.MaximumSupply);
        Assert.Empty(foo.Minters);

        var bar = new Currency("BAR", 0, 100, [AddressA, AddressB]);
        Assert.Equal("BAR", bar.Ticker);
        Assert.Equal(0, bar.DecimalPlaces);
        Assert.Equal(100, bar.MaximumSupply);
        Assert.Equal<Address>(bar.Minters, [AddressB, AddressA]);

        var baz = new Currency("baz", 1, [AddressA]);
        Assert.Equal("baz", baz.Ticker);
        Assert.Equal(1, baz.DecimalPlaces);
        Assert.Equal(0, baz.MaximumSupply);
        Assert.Equal<Address>(baz.Minters, [AddressA]);

        var qux = new Currency("QUX", 0);
        Assert.Equal("QUX", qux.Ticker);
        Assert.Equal(0, qux.MaximumSupply);
        Assert.Equal(0, qux.DecimalPlaces);
        Assert.Empty(qux.Minters);

        var quux = new Currency("QUUX", 3, 100);
        Assert.Equal("QUUX", quux.Ticker);
        Assert.Equal(3, quux.DecimalPlaces);
        Assert.Equal(100, quux.MaximumSupply);
        Assert.Empty(quux.Minters);

        Assert.Throws<ArgumentException>(() => new Currency(string.Empty, 0));
        Assert.Throws<ArgumentException>(() => new Currency("   \n", 1));
        Assert.Throws<ArgumentException>(() => new Currency("BAR", 1, [AddressA, AddressA]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Currency("TEST", 1, -100, []));
    }

    [Fact]
    public void Hash()
    {
        var currency = new Currency("GOLD", 2, [AddressA]);
        var expected = HashDigest<SHA1>.Parse("b74db4b0ce23048dd0523635711cf17e8c6ea6e6");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("NCG", 8, [AddressA, AddressB]);
        expected = HashDigest<SHA1>.Parse("585647629cbbb9552c621fa588297287d0c995b3");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("FOO", 0);
        expected = HashDigest<SHA1>.Parse("b4a20e835105e48a591c8afeba850262790dfffc");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("BAR", 1);
        expected = HashDigest<SHA1>.Parse("9f8707fb97946deda8514fc6be32317bc5422505");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("BAZ", 1);
        expected = HashDigest<SHA1>.Parse("8a354c6d2373a380c38b934bd83467c84cbc3fa5");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("BAZ", 1, 100);
        expected = HashDigest<SHA1>.Parse("9527eff9d3e55d1129c88cd8f9aad90c21958e1c");
        AssertBytesEqual(expected, currency.Hash);
    }

    [Fact]
    public void AllowsToMint()
    {
        var addressC = new PrivateKey().Address;
        var currency = new Currency("FOO", 0, [AddressA]);
        Assert.True(currency.CanMint(AddressA));
        Assert.False(currency.CanMint(AddressB));
        Assert.False(currency.CanMint(addressC));

        currency = new Currency("BAR", 2, [AddressA, AddressB]);
        Assert.True(currency.CanMint(AddressA));
        Assert.True(currency.CanMint(AddressB));
        Assert.False(currency.CanMint(addressC));

        currency = new Currency("BAZ", 0);
        Assert.True(currency.CanMint(AddressA));
        Assert.True(currency.CanMint(AddressB));
        Assert.True(currency.CanMint(addressC));

        currency = new Currency("QUX", 3, []);
        Assert.True(currency.CanMint(AddressA));
        Assert.True(currency.CanMint(AddressB));
        Assert.True(currency.CanMint(addressC));
    }

    [Fact]
    public void String()
    {
        var currency = new Currency("GOLD", 0, [AddressA]);
        Assert.Equal("GOLD (1a6193b06a2b6f80dba6b8800462451794f4b9f7)", currency.ToString());

        currency = new Currency("GOLD", 0, []);
        Assert.Equal("GOLD (c3dc908202a667bc47e330b8c5b4781425aec3d2)", currency.ToString());

        currency = new Currency("GOLD", 0);
        Assert.Equal("GOLD (c3dc908202a667bc47e330b8c5b4781425aec3d2)", currency.ToString());

        currency = new Currency("GOLD", 0, 100, [AddressA]);
        Assert.Equal("GOLD (07e4cf92498b170d91fa9b851212c92d8679c308)", currency.ToString());
    }

    [Fact]
    public void Equal()
    {
        var currencyA = new Currency("GOLD", 0, [AddressA]);
        var currencyB = new Currency("GOLD", 0, [AddressA]);
        var currencyC = new Currency("GOLD", 1, [AddressA]);
        var currencyD = new Currency("GOLD", 0, [AddressB]);
        var currencyE = new Currency("SILVER", 2, [AddressA]);
        var currencyF = new Currency("GOLD", 0, [AddressA]);
        var currencyG = new Currency("GOLD", 0, 100, [AddressA]);
        var currencyH = new Currency("GOLD", 0, 200, [AddressA]);
        var currencyI = new Currency("SILVER", 0, 200, [AddressA]);

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
        var foo = new Currency("FOO", 0);
        Assert.Equal(new FungibleAssetValue(foo, 123, 0), 123 * foo);
        Assert.Equal(new FungibleAssetValue(foo, -123, 0), foo * -123);

        var bar = new Currency("BAR", 2);
        Assert.Equal(new FungibleAssetValue(bar, 123, 1), 123.01m * bar);
    }

    [Fact]
    public void Serialize()
    {
        var foo = new Currency("FOO", 2);

        Assert.Equal(
            new List(
                new Text("FOO"),
                new Integer(2),
                new Integer(0),
                List.Empty),
            foo.ToBencodex());

        Assert.Equal(foo, Currency.Create(foo.ToBencodex()));

        var bar = new Currency("BAR", 0, 100, [AddressA, AddressB]);

        Assert.Equal(
            new List(
                new Text("BAR"),
                new Integer(0),
                new Integer(100),
                new List(
                    AddressB.ToBencodex(),
                    AddressA.ToBencodex())),
            bar.ToBencodex());

        Assert.Equal(bar, Currency.Create(bar.ToBencodex()));
    }

    [SkippableFact]
    public void JsonSerialization()
    {
        var foo = new Currency("FOO", 2);
        AssertJsonSerializable(foo, @"
            {
                ""hash"": ""b18bb67fceedc1f0664a4950138b7ef8e05f70e4"",
                ""ticker"": ""FOO"",
                ""decimalPlaces"": 2,
                ""maximumSupply"": ""0"",
                ""minters"": []
            }
        ");

        var bar = new Currency("BAR", 0, 100, [AddressA, AddressB]);
        AssertJsonSerializable(bar, @"
            {
                ""hash"": ""9ad8eac459906d7760e17e6b6e1a56d0bf87be5b"",
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
