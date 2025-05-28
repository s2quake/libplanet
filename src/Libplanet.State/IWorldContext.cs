using Libplanet.Types;

namespace Libplanet.State;

public partial interface IWorldContext
{
    IAccountContext this[string name] { get; }

    FungibleAssetValue GetBalance(Address address, Currency currency);

    void MintAsset(Address recipient, FungibleAssetValue value);

    void BurnAsset(Address owner, FungibleAssetValue value);

    void TransferAsset(Address sender, Address recipient, FungibleAssetValue value);
}
