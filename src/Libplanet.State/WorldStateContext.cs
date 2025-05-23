using Libplanet.Types.Assets;
using Libplanet.Types.Crypto;

namespace Libplanet.State;

public sealed class WorldStateContext(World world) : IWorldContext
{
    private readonly Dictionary<string, AccountStateContext> _accountByAddress = [];
    private readonly World _world = world;

    public AccountStateContext this[string name]
    {
        get
        {
            if (!_accountByAddress.TryGetValue(name, out var accountContext))
            {
                var account = _world.GetAccount(name);
                accountContext = new AccountStateContext(account, name);
                _accountByAddress[name] = accountContext;
            }

            return accountContext;
        }
    }

    IAccountContext IWorldContext.this[string name] => this[name];

    public FungibleAssetValue GetBalance(Address address, Currency currency) => _world.GetBalance(address, currency);

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
