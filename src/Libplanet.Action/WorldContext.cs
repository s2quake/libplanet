using Libplanet.Action.State;
using Libplanet.Types.Assets;
using Libplanet.Types.Crypto;

namespace Libplanet.Action;

internal sealed class WorldContext(World world) : IDisposable, IWorldContext
{
    private readonly Dictionary<string, AccountContext> _accountByName = [];
    private readonly HashSet<AccountContext> _dirtyAccounts = [];
    private World _world = world;
    private bool _disposed;

    public AccountContext this[string name]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_accountByName.TryGetValue(name, out var accountContext))
            {
                var account = _world.GetAccount(name);
                accountContext = new AccountContext(account, name, SetAccount);
                _accountByName[name] = accountContext;
            }

            return accountContext;
        }
    }

    IAccountContext IWorldContext.this[string name] => this[name];

    public World Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var accounts = _dirtyAccounts;
        foreach (var account in accounts)
        {
            _world = _world.SetAccount(account.Name, account.Account);
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
