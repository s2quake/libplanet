using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Crypto;

namespace Libplanet.Action;

public partial interface IWorldContext
{
    IAccountContext this[KeyBytes name] { get; }

    FungibleAssetValue GetBalance(Address address, Currency currency);

    void MintAsset(Address recipient, FungibleAssetValue value);

    void BurnAsset(Address owner, FungibleAssetValue value);

    void TransferAsset(Address sender, Address recipient, FungibleAssetValue value);
}
