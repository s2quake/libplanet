// using Libplanet.Action;
// using Libplanet.Data.Structures;
// using Libplanet.Types.Assets;
// using Libplanet.Types.Crypto;
// using static Libplanet.Types.ByteUtility;

// namespace Libplanet.Action.Tests.State;

// public class KeyConvertersTest
// {
//     [Fact]
//     public void ToKeysSpec()
//     {
//         var address = new PrivateKey().Address;
//         var currency = new Currency
//         {
//             Ticker = "Foo",
//             DecimalPlaces = 2,
//             Minters = [new PrivateKey().Address],
//         };

//         Assert.Equal(
//             (KeyBytes)Hex(address.Bytes),
//             KeyConverters.ToStateKey(address));

//         Assert.Equal(
//             (KeyBytes)$"_{Hex(address.Bytes)}_{Hex(currency.Hash.Bytes)}",
//             KeyConverters.ToFungibleAssetKey(address, currency));

//         Assert.Equal(
//             (KeyBytes)$"__{Hex(currency.Hash.Bytes)}",
//             KeyConverters.ToTotalSupplyKey(currency));

//         Assert.Equal(
//             (KeyBytes)"___",
//             KeyConverters.ValidatorSetKey);
//     }
// }
