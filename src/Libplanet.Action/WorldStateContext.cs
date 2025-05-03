using Libplanet.Action.State;
using Libplanet.Types.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Action;

public sealed class WorldStateContext(World world) : IWorldContext
{
    private readonly Dictionary<Address, AccountStateContext> _accountByAddress = [];
    private readonly World _world = world;

    public AccountStateContext this[Address address]
    {
        get
        {
            if (!_accountByAddress.TryGetValue(address, out var accountContext))
            {
                var account = _world.GetAccount(address);
                accountContext = new AccountStateContext(account, address);
                _accountByAddress[address] = accountContext;
            }

            return accountContext;
        }
    }

    IAccountContext IWorldContext.this[Address address] => this[address];

    public FungibleAssetValue GetBalance(Address address, Currency currency)
        => _world.GetBalance(address, currency);

    public void MintAsset(Address recipient, FungibleAssetValue value)
        => throw new NotSupportedException(
            $"{nameof(MintAsset)} is not supported in this context.");

    public void BurnAsset(Address owner, FungibleAssetValue value)
        => throw new NotSupportedException(
            $"{nameof(BurnAsset)} is not supported in this context.");

    public void TransferAsset(Address sender, Address recipient, FungibleAssetValue value)
        => throw new NotSupportedException(
            $"{nameof(TransferAsset)} is not supported in this context.");
}
