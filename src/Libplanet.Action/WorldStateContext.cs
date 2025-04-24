using Libplanet.Action.State;
// using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;

namespace Libplanet.Action;

public sealed class WorldStateContext(IWorldState world) : IWorldContext
{
    private readonly Dictionary<Address, AccountStateContext> _accountByAddress = [];
    private readonly IWorldState _world = world;

    public WorldStateContext(ITrie trie, IStateStore stateStore)
        : this(new WorldBaseState(trie, stateStore))
    {
    }

    // public WorldStateContext(BlockChain blockChain)
    //     : this(blockChain.GetNextWorldState() ?? blockChain.GetWorldState())
    // {
    // }

    public bool IsReadOnly => true;

    public AccountStateContext this[Address address]
    {
        get
        {
            if (!_accountByAddress.TryGetValue(address, out var accountContext))
            {
                var account = _world.GetAccountState(address);
                accountContext = new AccountStateContext(account, address);
                _accountByAddress[address] = accountContext;
            }

            return accountContext;
        }
    }

    IAccountContext IWorldContext.this[Address address] => this[address];

    public FungibleAssetValue GetBalance(Address address, Currency currency)
        => _world.GetBalance(address, currency);

    public void MintAsset(FungibleAssetValue value)
        => throw new NotSupportedException(
            $"{nameof(MintAsset)} is not supported in this context.");

    public void TransferAsset(Address sender, Address recipient, FungibleAssetValue value)
        => throw new NotSupportedException(
            $"{nameof(TransferAsset)} is not supported in this context.");
}
