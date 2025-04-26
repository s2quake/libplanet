using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Action;

internal sealed class WorldContext(IActionContext context) : IDisposable, IWorldContext
{
    private readonly Dictionary<Address, AccountContext> _accountByAddress = [];
    private readonly HashSet<AccountContext> _dirtyAccounts = [];
    private IWorld _world = context.World;
    private bool _disposed;

    public bool IsReadOnly => false;

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

    public IWorld Flush()
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

    public void MintAsset(FungibleAssetValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _world = _world.MintAsset(context, context.Signer, value);
    }

    public void TransferAsset(Address sender, Address recipient, FungibleAssetValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _world = _world.TransferAsset(context, sender, recipient, value);
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
