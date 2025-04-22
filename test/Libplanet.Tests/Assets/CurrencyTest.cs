using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Xunit;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Assets
{
    public class CurrencyTest
    {
        public static readonly Address AddressA =
            Address.Parse("D6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9");

        public static readonly Address AddressB =
            Address.Parse("5003712B63baAB98094aD678EA2B24BcE445D076");

        [Fact]
        public void Constructor()
        {
#pragma warning disable CS0618  // must test obsoleted new Currency() for backwards compatibility
            var foo = new Currency("FOO", 2);
#pragma warning restore CS0618  // must test obsoleted new Currency() for backwards compatibility
            Assert.Equal("FOO", foo.Ticker);
            Assert.Equal(2, foo.DecimalPlaces);
            Assert.Equal(0, foo.MaximumSupply);
            Assert.Empty(foo.Minters);
            Assert.False(foo.IsTrackable);

            var bar = new Currency("\t BAR \n", 0, 100, [AddressA, AddressB]);
            Assert.Equal("BAR", bar.Ticker);
            Assert.Equal(0, bar.DecimalPlaces);
            Assert.Equal(100, bar.MaximumSupply);
            Assert.Equal(bar.Minters, [AddressA, AddressB]);
            Assert.True(bar.IsTrackable);

            var baz = new Currency("baz", 1, [AddressA]);
            Assert.Equal("baz", baz.Ticker);
            Assert.Equal(1, baz.DecimalPlaces);
            Assert.Null(baz.MaximumSupply);
            Assert.Equal(baz.Minters, [AddressA]);
            Assert.True(baz.IsTrackable);

            var qux = new Currency("QUX", 0);
            Assert.Equal("QUX", qux.Ticker);
            Assert.Null(qux.MaximumSupply);
            Assert.Equal(0, qux.DecimalPlaces);
            Assert.Null(qux.Minters);
            Assert.True(qux.IsTrackable);

            var quux = new Currency("QUUX", 3, 100);
            Assert.Equal("QUUX", quux.Ticker);
            Assert.Equal(3, quux.DecimalPlaces);
            Assert.Equal(100, quux.MaximumSupply);
            Assert.Empty(quux.Minters);
            Assert.True(qux.IsTrackable);

            Assert.Throws<ArgumentException>(() =>
                new Currency(string.Empty, 0)
            );
            Assert.Throws<ArgumentException>(() =>
                new Currency("   \n", 1)
            );
            Assert.Throws<ArgumentException>(() =>
                new Currency("TEST", 1, -100, [])
            );
        }

        [Fact]
        public void Hash()
        {
#pragma warning disable CS0618  // must test obsoleted new Currency() for backwards compatibility
            Currency currency = new Currency("GOLD", 2, [AddressA]);
#pragma warning restore CS0618  // must test obsoleted new Currency() for backwards compatibility
            HashDigest<SHA1> expected =
                HashDigest<SHA1>.Parse("81446cd346c1be9e686835742bfd3772194dea21");
            AssertBytesEqual(expected, currency.Hash);

#pragma warning disable CS0618  // must test obsoleted new Currency() for backwards compatibility
            currency = new Currency("NCG", 8, [AddressA, AddressB]);
#pragma warning restore CS0618  // must test obsoleted new Currency() for backwards compatibility
            expected = HashDigest<SHA1>.Parse("42ce3a098fe14084e89d3d4449f56126693aeed1");
            AssertBytesEqual(expected, currency.Hash);

#pragma warning disable CS0618  // must test obsoleted new Currency() for backwards compatibility
            currency = new Currency("FOO", 0);
#pragma warning restore CS0618  // must test obsoleted new Currency() for backwards compatibility
            expected = HashDigest<SHA1>.Parse("801990ea2885bd51eebca0e826cc0e27f0917a9b");
            AssertBytesEqual(expected, currency.Hash);

#pragma warning disable CS0618  // must test obsoleted new Currency() for backwards compatibility
            currency = new Currency("BAR", 1);
#pragma warning restore CS0618  // must test obsoleted new Currency() for backwards compatibility
            expected = HashDigest<SHA1>.Parse("da42781871890f1e1b7d6f49c7f2733d3ba7b8bd");
            AssertBytesEqual(expected, currency.Hash);

            currency = new Currency("BAZ", 1);
            expected = HashDigest<SHA1>.Parse("d7fe111cae5b2503939c9bce864ca3b64d575e8d");
            AssertBytesEqual(expected, currency.Hash);

            currency = new Currency("BAZ", 1, 100);
            expected = HashDigest<SHA1>.Parse("38bd85ea71c09ca7ed82b61fe91bc205101db191");
            AssertBytesEqual(expected, currency.Hash);
        }

        [Fact]
        public void AllowsToMint()
        {
            Address addressC = new PrivateKey().Address;
            Currency currency = new Currency("FOO", 0, [AddressA]);
            Assert.True(currency.CanMint(AddressA));
            Assert.False(currency.CanMint(AddressB));
            Assert.False(currency.CanMint(addressC));

            currency = new Currency("BAR", 2, [AddressA, AddressB]);
            Assert.True(currency.CanMint(AddressA));
            Assert.True(currency.CanMint(AddressB));
            Assert.False(currency.CanMint(addressC));

            currency = new Currency("BAZ", 0);
            Assert.False(currency.CanMint(AddressA));
            Assert.False(currency.CanMint(AddressB));
            Assert.False(currency.CanMint(addressC));

            currency = new Currency("QUX", 3);
            Assert.True(currency.CanMint(AddressA));
            Assert.True(currency.CanMint(AddressB));
            Assert.True(currency.CanMint(addressC));
        }

        [Fact]
        public void String()
        {
#pragma warning disable CS0618  // must test obsoleted new Currency() for backwards compatibility
            Currency currency = new Currency("GOLD", 0, [AddressA]);
#pragma warning restore CS0618  // must test obsoleted new Currency() for backwards compatibility
            Assert.Equal("GOLD (688ded7b7ae6e551e14e58ec23fef3540d442a35)", currency.ToString());

            currency = new Currency("GOLD", 0, [AddressA]);
            Assert.Equal("GOLD (a418b63455695d932ed154d5ce59575ea7cbb1f0)", currency.ToString());

            currency = new Currency("GOLD", 0, 100, [AddressA]);
            Assert.Equal("GOLD (f0fd50b87b39d9f24c0922551e59914dbbeb3544)", currency.ToString());
        }

        [Fact]
        public void Equal()
        {
            var currencyA = new Currency("GOLD", 0, [AddressA]);
            var currencyB = new Currency("GOLD", 0, [AddressA]);
            var currencyC = new Currency("GOLD", 1, [AddressA]);
            var currencyD = new Currency("GOLD", 0, [AddressB]);
            var currencyE = new Currency("SILVER", 2, [AddressA]);
#pragma warning disable CS0618  // must test obsoleted new Currency() for backwards compatibility
            var currencyF = new Currency("GOLD", 0, [AddressA]);
#pragma warning restore CS0618  // must test obsoleted new Currency() for backwards compatibility
            var currencyG = new Currency("GOLD", 0, 100, [AddressA]);
            var currencyH = new Currency("GOLD", 0, 200, [AddressA]);
            var currencyI = new Currency("SILVER", 0, 200, [AddressA]);

            Assert.Equal(currencyA, currencyA);
            Assert.Equal(currencyA, currencyB);
            Assert.NotEqual(currencyA, currencyC);
            Assert.NotEqual(currencyA, currencyD);
            Assert.NotEqual(currencyA, currencyE);
            Assert.NotEqual(currencyA, currencyF);
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
#pragma warning disable CS0618  // must test obsoleted new Currency() for backwards compatibility
            var foo = new Currency("FOO", 2);
#pragma warning restore CS0618  // must test obsoleted new Currency() for backwards compatibility

            Assert.Equal(
                Dictionary.Empty
                    .Add("ticker", "FOO")
                    .Add("decimalPlaces", new byte[] { 2 })
                    .Add("minters", Null.Value),
                foo.ToBencodex());

            Assert.Equal(foo, Currency.Create(foo.ToBencodex()));

            var bar =
                new Currency("BAR", 0, 100, [AddressA, AddressB]);

            Assert.Equal(
                Dictionary.Empty
                    .Add("ticker", "BAR")
                    .Add("decimalPlaces", new byte[] { 0 })
                    .Add("maximumSupplyMajor", 100)
                    .Add("maximumSupplyMinor", 0)
                    .Add(
                        "minters",
                        List.Empty.Add(AddressB.ToByteArray()).Add(AddressA.ToByteArray()))
                    .Add("totalSupplyTrackable", true),
                bar.ToBencodex());

            Assert.Equal(bar, Currency.Create(bar.ToBencodex()));
        }

        [SkippableFact]
        public void JsonSerialization()
        {
#pragma warning disable CS0618  // must test obsoleted new Currency() for backwards compatibility
            var foo = new Currency("FOO", 2);
#pragma warning restore CS0618  // must test obsoleted new Currency() for backwards compatibility
            AssertJsonSerializable(foo, @"
                {
                    ""hash"": ""8db87f973776e2218113202e00e09e185fff8971"",
                    ""ticker"": ""FOO"",
                    ""decimalPlaces"": 2,
                    ""minters"": null,
                    ""maximumSupply"": null,
                    ""totalSupplyTrackable"": false,
                }
            ");

            var bar =
                new Currency("BAR", 0, 100, [AddressA, AddressB]);
            AssertJsonSerializable(bar, @"
                {
                    ""hash"": ""e4ee30562819a9e74be40098c76f84209d05da5e"",
                    ""ticker"": ""BAR"",
                    ""decimalPlaces"": 0,
                    ""minters"": [
                        ""5003712B63baAB98094aD678EA2B24BcE445D076"",
                        ""D6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9"",
                    ],
                    ""maximumSupply"": ""100.0"",
                    ""totalSupplyTrackable"": true,
                }
            ");
        }
    }
}
