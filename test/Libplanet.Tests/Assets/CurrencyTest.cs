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
        Assert.False(foo.IsTrackable);

        var bar = new Currency("BAR", 0, 100, [AddressA, AddressB]) { IsTrackable = true };
        Assert.Equal("BAR", bar.Ticker);
        Assert.Equal(0, bar.DecimalPlaces);
        Assert.Equal(100, bar.MaximumSupply);
        Assert.Equal<Address>(bar.Minters, [AddressB, AddressA]);
        Assert.True(bar.IsTrackable);

        var baz = new Currency("baz", 1, [AddressA]) { IsTrackable = true };
        Assert.Equal("baz", baz.Ticker);
        Assert.Equal(1, baz.DecimalPlaces);
        Assert.Equal(0, baz.MaximumSupply);
        Assert.Equal<Address>(baz.Minters, [AddressA]);
        Assert.True(baz.IsTrackable);

        var qux = new Currency("QUX", 0) { IsTrackable = true };
        Assert.Equal("QUX", qux.Ticker);
        Assert.Equal(0, qux.MaximumSupply);
        Assert.Equal(0, qux.DecimalPlaces);
        Assert.Empty(qux.Minters);
        Assert.True(qux.IsTrackable);

        var quux = new Currency("QUUX", 3, 100) { IsTrackable = true };
        Assert.Equal("QUUX", quux.Ticker);
        Assert.Equal(3, quux.DecimalPlaces);
        Assert.Equal(100, quux.MaximumSupply);
        Assert.Empty(quux.Minters);
        Assert.True(qux.IsTrackable);

        Assert.Throws<ArgumentException>(() => new Currency(string.Empty, 0));
        Assert.Throws<ArgumentException>(() => new Currency("   \n", 1));
        Assert.Throws<ArgumentException>(() => new Currency("BAR", 1, [AddressA, AddressA]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Currency("TEST", 1, -100, []));
    }

    [Fact]
    public void Hash()
    {
        var currency = new Currency("GOLD", 2, [AddressA]);
        var expected = HashDigest<SHA1>.Parse("3468baa41800b24d3b3b8d22b6b66808a00c6d30");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("NCG", 8, [AddressA, AddressB]);
        expected = HashDigest<SHA1>.Parse("6e7bea7d64de1a3e1cd9debaa979906cb60eafe0");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("FOO", 0);
        expected = HashDigest<SHA1>.Parse("be81aec7bf9bf06aede3353c041c0bade9b2a328");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("BAR", 1);
        expected = HashDigest<SHA1>.Parse("b6680f750b76fe91795b46e51e4ff8839c36a7c4");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("BAZ", 1);
        expected = HashDigest<SHA1>.Parse("c852169ea14fff33fd21acd79709bfed2ef63ec6");
        AssertBytesEqual(expected, currency.Hash);

        currency = new Currency("BAZ", 1, 100);
        expected = HashDigest<SHA1>.Parse("912979876d9b483f4cb96bba37bc9b274c387661");
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
        Assert.Equal("GOLD (8b57f3742306bd17af6aa888a97f39299aab37fe)", currency.ToString());

        currency = new Currency("GOLD", 0, [AddressA]);
        Assert.Equal("GOLD (8b57f3742306bd17af6aa888a97f39299aab37fe)", currency.ToString());

        currency = new Currency("GOLD", 0, 100, [AddressA]);
        Assert.Equal("GOLD (0dd7d3783993eeefbff2a5b23b6dc04a6a82436b)", currency.ToString());
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
                List.Empty,
                new Bencodex.Types.Boolean(false)),
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
                    AddressA.ToBencodex()),
                new Bencodex.Types.Boolean(false)),
            bar.ToBencodex());

        Assert.Equal(bar, Currency.Create(bar.ToBencodex()));
    }

    [SkippableFact]
    public void JsonSerialization()
    {
        var foo = new Currency("FOO", 2);
        AssertJsonSerializable(foo, @"
            {
                ""hash"": ""f5b3fa6590b1ab33c833f7b81fc1498b262ae2cd"",
                ""ticker"": ""FOO"",
                ""decimalPlaces"": 2,
                ""maximumSupply"": ""0"",
                ""minters"": [],
                ""isTrackable"": false,
            }
        ");

        var bar = new Currency("BAR", 0, 100, [AddressA, AddressB]) { IsTrackable = true };
        AssertJsonSerializable(bar, @"
                {
                    ""hash"": ""7c49633bf327762281895760a10ce31e9c3bac6d"",
                    ""ticker"": ""BAR"",
                    ""decimalPlaces"": 0,
                    ""maximumSupply"": ""100"",
                    ""minters"": [
                        ""0x5003712B63baAB98094aD678EA2B24BcE445D076"",
                        ""0xD6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9"",
                    ],
                    ""isTrackable"": true,
                }
            ");
    }
}
