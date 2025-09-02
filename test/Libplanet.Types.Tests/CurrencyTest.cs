using System.Diagnostics;
using Libplanet.Serialization;
using Libplanet.TestUtilities;

namespace Libplanet.Types.Tests;

public class CurrencyTest
{
    public static readonly Address AddressA = Address.Parse("D6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9");

    public static readonly Address AddressB = Address.Parse("5003712B63baAB98094aD678EA2B24BcE445D076");

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

        ModelAssert.Throws(Currency.Create(string.Empty, 0), nameof(Currency.Ticker));
        ModelAssert.Throws(Currency.Create("   \n", 1), nameof(Currency.Ticker));
        ModelAssert.Throws(Currency.Create("bar", 1), nameof(Currency.Ticker));
        ModelAssert.Throws(Currency.Create("TEST", 1, -100, []), nameof(Currency.MaximumSupply));
    }

    [Fact]
    public void Hash()
    {
        (string, Currency)[] items =
        [
            ("5c167c93e9d55301c126b533f3f9bfaf7c8035c6", Currency.Create("GOLD", 2, [AddressA])),
            ("bdec9f36c8c990ca5f5af5142f220b3560a39e7a", Currency.Create("NCG", 8, [AddressA, AddressB])),
            ("ad8dce7d6f7aa1625318743d162f6d386f022a14", Currency.Create("FOO", 0)),
            ("a16139726848bc16b208d62efa0f3b20a2468ac8", Currency.Create("BAR", 1)),
            ("46943167967e11a862b08a16df44b9ba48aa1b5f", Currency.Create("BAZ", 1)),
            ("02e5cdc825bd2f27ba506a5996667c1cc9bd9653", Currency.Create("BAZ", 1, 100)),
        ];

#if DEBUG
        for (var i = 0; i < items.Length; i++)
        {
            var (_, currency) = items[i];
            Trace.WriteLine($"{currency.Hash}, {currency}");
        }
#endif // DEBUG

        for (var i = 0; i < items.Length; i++)
        {
            var (expectedHash, currency) = items[i];
            Assert.Equal(expectedHash, currency.Hash.ToString());
        }
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
        (string, Currency)[] items =
        [
            ("GOLD (36bbe96591f0c209addbd038bc2d912f18ef9c48)", Currency.Create("GOLD", 0, [AddressA])),
            ("GOLD (b0ec285863ae0fd90fbbca482b529c0c42309b02)", Currency.Create("GOLD", 0, [])),
            ("GOLD (7713ebfc5c5b3a32dc279e671dcb771958ac82ae)", Currency.Create("GOLD", 0, 100, [AddressA])),
            ("GOLD (b0ec285863ae0fd90fbbca482b529c0c42309b02)", Currency.Create("GOLD", 0)),
            ("FOO (ad8dce7d6f7aa1625318743d162f6d386f022a14)", Currency.Create("FOO", 0)),
            ("BAR (a16139726848bc16b208d62efa0f3b20a2468ac8)", Currency.Create("BAR", 1)),
            ("BAZ (46943167967e11a862b08a16df44b9ba48aa1b5f)", Currency.Create("BAZ", 1)),
            ("BAZ (02e5cdc825bd2f27ba506a5996667c1cc9bd9653)", Currency.Create("BAZ", 1, 100)),
        ];

#if DEBUG
        for (var i = 0; i < items.Length; i++)
        {
            var (_, currency) = items[i];
            Trace.WriteLine($"{currency.Hash}, {currency}");
        }
#endif // DEBUG

        for (var i = 0; i < items.Length; i++)
        {
            var (expectedString, currency) = items[i];
            Assert.Equal(expectedString, currency.ToString());
        }
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
    public void GetRawValue()
    {
        var currency = Currency.Create("FOO", 2);
        Assert.Equal(12300, currency.GetRawValue(123, 0));
        Assert.Equal(12301, currency.GetRawValue(123, 1));
        Assert.Equal(12302, currency.GetRawValue(123, 2));
        Assert.Equal(12321, currency.GetRawValue(123, 21));
        Assert.Equal(12399, currency.GetRawValue(123, 99));
        Assert.Equal(12299, currency.GetRawValue(123, -1));
        Assert.Equal(12279, currency.GetRawValue(123, -21));
        Assert.Equal(12201, currency.GetRawValue(123, -99));
    }

    [Fact]
    public void GetRawValue_Throw()
    {
        var currency = Currency.Create("FOO", 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => currency.GetRawValue(123, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => currency.GetRawValue(123, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => currency.GetRawValue(123, -100));
        Assert.Throws<ArgumentOutOfRangeException>(() => currency.GetRawValue(123, -101));
    }
}
