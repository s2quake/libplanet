using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Action;

internal sealed class WorldContext(World world) : IDisposable, IWorldContext
{
    private readonly Dictionary<Address, AccountContext> _accountByAddress = [];
    private readonly HashSet<AccountContext> _dirtyAccounts = [];
    private World _world = world;
    private bool _disposed;

    public AccountContext this[Address address]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_accountByAddress.TryGetValue(address, out var accountContext))
            {
                var account = _world.GetAccount(address);
                accountContext = new AccountContext(account, address, SetAccount);
                _accountByAddress[address] = accountContext;
            }

            return accountContext;
        }
    }

    IAccountContext IWorldContext.this[Address address] => this[address];

    public World Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var accounts = _dirtyAccounts;
        foreach (var account in accounts)
        {
            _world = _world.SetAccount(account.Address, account.Account);
        }

        _dirtyAccounts.Clear();
        return _world;
    }

    public FungibleAssetValue GetBalance(Address address, Currency currency)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _world.GetBalance(address, currency);
    }

    public void MintAsset(Address recipient, FungibleAssetValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _world = _world.MintAsset(recipient, value);
    }

    public void BurnAsset(Address owner, FungibleAssetValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _world = _world.BurnAsset(owner, value);
    }

    public void TransferAsset(Address sender, Address recipient, FungibleAssetValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _world = _world.TransferAsset(sender, recipient, value);
    }

    public void SetDirty(AccountContext accountContext)
        => _dirtyAccounts.Add(accountContext);

    void IDisposable.Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void SetAccount(AccountContext accountContext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _dirtyAccounts.Add(accountContext);
    }
}
