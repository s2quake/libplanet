using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;

namespace Libplanet.Action.State.Tests
{
    public class KeyConvertersTest
    {
        public KeyConvertersTest()
        {
        }

        [Fact]
        public void ToKeysSpec()
        {
            var address = new PrivateKey().Address;
            var currency = new Currency
            {
                Ticker = "Foo",
                DecimalPlaces = 2,
                Minters = [new PrivateKey().Address],
            };

            Assert.Equal(
                (KeyBytes)ByteUtil.Hex(address.Bytes),
                KeyConverters.ToStateKey(address));

            Assert.Equal(
                (KeyBytes)
                    $"_{ByteUtil.Hex(address.Bytes)}_{ByteUtil.Hex(currency.Hash.Bytes)}",
                KeyConverters.ToFungibleAssetKey(address, currency));

            Assert.Equal(
                (KeyBytes)$"__{ByteUtil.Hex(currency.Hash.Bytes)}",
                KeyConverters.ToTotalSupplyKey(currency));

            Assert.Equal(
                (KeyBytes)"___",
                KeyConverters.ValidatorSetKey);
        }
    }
}
