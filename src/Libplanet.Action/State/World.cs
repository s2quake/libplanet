using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public sealed record class World : IWorld
{
    private readonly IStateStore _stateStore;
    private readonly ImmutableDictionary<Address, Account> _accountByAddress;

    public World(Address signer, ITrie trie, IStateStore stateStore)
        : this(signer, trie, stateStore, ImmutableDictionary<Address, Account>.Empty)
    {
    }

    public World(Address signer, HashDigest<SHA256> stateRootHash, IStateStore stateStore)
        : this(signer, stateStore.GetStateRoot(stateRootHash), stateStore, ImmutableDictionary<Address, Account>.Empty)
    {
    }

    private World(Address signer, ITrie trie, IStateStore stateStore, ImmutableDictionary<Address, Account> delta)
    {
        Signer = signer;
        Trie = trie;
        _stateStore = stateStore;
        _accountByAddress = delta;
    }

    public ITrie Trie { get; }

    public Address Signer { get; }

    public int Version => Trie.GetMetadata() is { } value ? value.Version : 0;

    ImmutableDictionary<Address, IAccount> IWorld.Delta
        => _accountByAddress.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => (IAccount)kvp.Value);

    public Account GetAccount(Address address)
    {
        if (_accountByAddress.TryGetValue(address, out var account))
        {
            return account;
        }

        if (Trie.TryGetValue(ToStateKey(address), out var value) && value is Binary binary)
        {
            return new Account(_stateStore.GetStateRoot(new HashDigest<SHA256>(binary.ByteArray)));
        }
        else
        {
            return new Account(_stateStore.GetStateRoot(default));
        }
    }

    public World SetAccount(Address address, Account account)
        => new(Trie, _stateStore, _accountByAddress.SetItem(address, account));

    IAccount IWorld.GetAccount(Address address) => GetAccount(address);

    IWorld IWorld.SetAccount(Address address, IAccount account) => SetAccount(address, (Account)account);
}
