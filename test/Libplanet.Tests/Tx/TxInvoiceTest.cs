using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Tests.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using Xunit;

namespace Libplanet.Tests.Tx
{
    public class TxInvoiceTest
    {
        public static readonly Address AddressA =
            Address.Parse("D6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9");

        public static readonly Address AddressB =
            Address.Parse("B61CE2Ce6d28237C1BC6E114616616762f1a12Ab");

        [Fact]
        public void ConstructorGasConditions()
        {
            var random = new System.Random();
            var genesisHash = random.NextBlockHash();
            var timestamp = DateTimeOffset.UtcNow;
            var actions = ImmutableArray<IValue>.Empty;

            _ = new TxInvoice
            {
                GenesisHash = genesisHash,
                Timestamp = timestamp,
                Actions = actions,
            };
            _ = new TxInvoice
            {
                GenesisHash = genesisHash,
                Timestamp = timestamp,
                Actions = actions,
                MaxGasPrice = new Currency("DUMB", 0) * 100,
                GasLimit = 100,
            };

            Assert.Throws<ArgumentException>(() =>
                new TxInvoice
                {
                    GenesisHash = genesisHash,
                    Timestamp = timestamp,
                    Actions = actions,
                    MaxGasPrice = new Currency("DUMB", 0) * 100,
                }.Verify());
            Assert.Throws<ArgumentException>(() =>
                new TxInvoice
                {
                    GenesisHash = genesisHash,
                    Timestamp = timestamp,
                    Actions = actions,
                    GasLimit = 100,
                }.Verify());
            Assert.Throws<ArgumentException>(() =>
                new TxInvoice
                {
                    GenesisHash = genesisHash,
                    Timestamp = timestamp,
                    Actions = actions,
                    MaxGasPrice = new Currency("DUMB", 0) * -100,
                    GasLimit = 100,
                }.Verify());
            Assert.Throws<ArgumentException>(() =>
                new TxInvoice
                {
                    GenesisHash = genesisHash,
                    Timestamp = timestamp,
                    Actions = actions,
                    MaxGasPrice = new Currency("DUMB", 0) * 100,
                    GasLimit = -100,
                }.Verify());
        }

        [Fact]
        public void PlainConstructor()
        {
            var random = new System.Random();
            var genesisHash = random.NextBlockHash();
            var updatedAddresses = ImmutableSortedSet.Create(
                random.NextAddress(),
                random.NextAddress());
            var timestamp = DateTimeOffset.UtcNow;
            var actions = ImmutableArray.Create<IAction>([
                DumbAction.Create((random.NextAddress(), "foo")),
                DumbAction.Create((random.NextAddress(), "bar")),
            ]).ToPlainValues().ToImmutableArray();
            var invoice = new TxInvoice
            {
                GenesisHash = genesisHash,
                UpdatedAddresses = updatedAddresses,
                Timestamp = timestamp,
                Actions = actions,
            };
            Assert.Equal(genesisHash, invoice.GenesisHash);
            Assert.True(updatedAddresses.SetEquals(invoice.UpdatedAddresses));
            Assert.Equal(timestamp, invoice.Timestamp);
            Assert.Equal(actions, invoice.Actions);
        }

        [Fact]
        public void ShortcutConstructor()
        {
            var before = DateTimeOffset.UtcNow;
            var invoice = new TxInvoice();
            var after = DateTimeOffset.UtcNow;
            Assert.Null(invoice.GenesisHash);
            Assert.Empty(invoice.UpdatedAddresses);
            Assert.InRange(invoice.Timestamp, before, after);
            Assert.Empty(invoice.Actions);
        }

        [Fact]
        public void CopyConstructor()
        {
            var random = new System.Random();
            var genesisHash = random.NextBlockHash();
            var updatedAddresses = ImmutableSortedSet.Create(
                random.NextAddress(),
                random.NextAddress());
            var timestamp = DateTimeOffset.UtcNow;
            var actions = ImmutableArray.Create<IAction>([
                DumbAction.Create((random.NextAddress(), "foo")),
                DumbAction.Create((random.NextAddress(), "bar")),
            ]).ToPlainValues().ToImmutableArray();
            var original = new TxInvoice
            {
                GenesisHash = genesisHash,
                UpdatedAddresses = updatedAddresses,
                Timestamp = timestamp,
                Actions = actions,
            };
            var copy = original with { };
            Assert.Equal(genesisHash, copy.GenesisHash);
            Assert.True(updatedAddresses.SetEquals(copy.UpdatedAddresses));
            Assert.Equal(timestamp, copy.Timestamp);
            Assert.Equal(actions, copy.Actions);
        }

        [Fact]
        public void Equality()
        {
            var genesisHash = BlockHash.Parse(
                "92854cf0a62a7103b9c610fd588ad45254e64b74ceeeb209090ba572a41bf265");
            var updatedAddresses = ImmutableSortedSet.Create(AddressA, AddressB);
            var timestamp = new DateTimeOffset(2023, 3, 29, 1, 2, 3, 456, TimeSpan.Zero);
            var actions = ImmutableArray.Create<IAction>([
                DumbAction.Create((AddressA, "foo")),
                DumbAction.Create((AddressB, "bar")),
            ]).ToPlainValues().ToImmutableArray();
            var invoice1 = new TxInvoice
            {
                GenesisHash = genesisHash,
                UpdatedAddresses = updatedAddresses,
                Timestamp = timestamp,
                Actions = actions,
            };
            var invoice2 = new TxInvoice
            {
                GenesisHash = genesisHash,
                UpdatedAddresses = updatedAddresses,
                Timestamp = timestamp,
                Actions = actions,
            };
            Assert.True(invoice1.Equals(invoice2));
            Assert.True(invoice1.Equals((object)invoice2));
            Assert.Equal(invoice1.GetHashCode(), invoice2.GetHashCode());

            Assert.False(invoice1.Equals(null));

            for (int i = 0; i < 5; i++)
            {
                // NOTE: Non-null cases for MaxGasPrice and GasLimit are flipped as existing
                // mock object has respective values set to null.
                var invoice = new TxInvoice
                {
                    GenesisHash = i == 0 ? default : genesisHash,
                    UpdatedAddresses = i == 1 ? [] : updatedAddresses,
                    Timestamp = i == 2 ? DateTimeOffset.MinValue : timestamp,
                    Actions = i == 3 ? [] : actions,
                    MaxGasPrice = i == 4
                        ? new FungibleAssetValue(
                            new Currency("FOO", 18, [new PrivateKey().Address]),
                            100)
                        : null,
                    GasLimit = i == 4 ? 10 : 0L,
                };
                Assert.False(invoice1.Equals(invoice));
                Assert.False(invoice1.Equals((object)invoice));
                Assert.NotEqual(invoice1.GetHashCode(), invoice.GetHashCode());
            }
        }

        [Fact]
        public void JsonSerialization()
        {
            var genesisHash = BlockHash.Parse(
                "92854cf0a62a7103b9c610fd588ad45254e64b74ceeeb209090ba572a41bf265");
            var updatedAddresses = ImmutableSortedSet.Create(AddressA, AddressB);
            var timestamp = new DateTimeOffset(2023, 3, 29, 1, 2, 3, 456, TimeSpan.Zero);
            var actions = ImmutableArray.Create<IAction>([
                DumbAction.Create((AddressA, "foo")),
                DumbAction.Create((AddressB, "bar")),
            ]).ToPlainValues().ToImmutableArray();
            var invoice = new TxInvoice
            {
                GenesisHash = genesisHash,
                UpdatedAddresses = updatedAddresses,
                Timestamp = timestamp,
                Actions = actions,
                MaxGasPrice = new FungibleAssetValue(
                    new Currency("FOO", 18, [AddressA]),
                    1234,
                    5678),
                GasLimit = 100,
            };
#pragma warning disable MEN002  // Long lines are OK for test JSON data.
            TestUtils.AssertJsonSerializable(
                invoice,
                $@"
                    {{
                      ""genesisHash"": ""92854cf0a62a7103b9c610fd588ad45254e64b74ceeeb209090ba572a41bf265"",
                      ""updatedAddresses"": [
                        ""B61CE2Ce6d28237C1BC6E114616616762f1a12Ab"",
                        ""D6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9""
                      ],
                      ""timestamp"": ""2023-03-29T01:02:03.456\u002B00:00"",
                      ""actions"": [
                        {{
                          ""\uFEFFitem"": ""\uFEFFfoo"",
                          ""\uFEFFtarget_address"": ""0xd6d639da5a58a78a564c2cd3db55fa7cebe244a9"",
                          ""\uFEFFtype_id"": ""\uFEFFDumbAction""
                        }},
                        {{
                          ""\uFEFFitem"": ""\uFEFFbar"",
                          ""\uFEFFtarget_address"": ""0xb61ce2ce6d28237c1bc6e114616616762f1a12ab"",
                          ""\uFEFFtype_id"": ""\uFEFFDumbAction""
                        }}
                      ],
                      ""maxGasPrice"": {{
                        ""quantity"": ""1234.000000000000005678"",
                        ""currency"": {{
                          ""maximumSupply"": null,
                          ""ticker"": ""FOO"",
                          ""decimalPlaces"": 18,
                          ""minters"": [
                            ""D6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9""
                          ],
                          ""hash"": ""a0d19219acb8d69815b3d299393c48bc8a93e0ec""
                        }}
                      }},
                      ""gasLimit"": 100,
                    }}
                ",
                false);
#pragma warning restore MEN002
        }
    }
}
