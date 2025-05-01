using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public sealed record class World
{
    // private readonly IStateStore _stateStore;
    // private readonly ImmutableDictionary<Address, Account> _accountByAddress;

    // public World(Address signer, ITrie trie, IStateStore stateStore)
    //     : this(signer, trie, stateStore, ImmutableDictionary<Address, Account>.Empty)
    // {
    // }

    // private World(Address signer, ITrie trie, IStateStore stateStore, ImmutableDictionary<Address, Account> delta)
    // {
    //     Signer = signer;
    //     Trie = trie;
    //     _stateStore = stateStore;
    //     _accountByAddress = delta;
    // }

    public required ITrie Trie { get; init; }

    public required IStateStore StateStore { get; init; }

    public Address Signer { get; init; }

    public int Version { get; init; }

    public ImmutableDictionary<Address, Account> Delta { get; private init; } = ImmutableDictionary<Address, Account>.Empty;

    // public int Version => Trie.GetMetadata() is { } value ? value.Version : 0;

    // ImmutableDictionary<Address, Account> World.Delta
    //     => _accountByAddress.ToImmutableDictionary(
    //         kvp => kvp.Key,
    //         kvp => (Account)kvp.Value);

    public static World Create() => Create(new TrieStateStore());

    public static World Create(IStateStore stateStore) => Create(stateStore.GetStateRoot(default), stateStore);

    public static World Create(ITrie trie, IStateStore stateStore) => new()
    {
        Trie = trie,
        StateStore = stateStore,
    };

    public static World Create(HashDigest<SHA256> stateRootHash, IStateStore stateStore) => new()
    {
        Trie = stateStore.GetStateRoot(stateRootHash),
        StateStore = stateStore,
    };

    public Account GetAccount(Address address)
    {
        if (Delta.TryGetValue(address, out var account))
        {
            return account;
        }

        if (Trie.TryGetValue(ToStateKey(address), out var value) && value is Binary binary)
        {
            return new Account(StateStore.GetStateRoot(new HashDigest<SHA256>(binary.ByteArray)));
        }
        else
        {
            return new Account(StateStore.GetStateRoot(default));
        }
    }

    public World SetAccount(Address address, Account account) => this with
    {
        Delta = Delta.SetItem(address, account),
    };

    // Account World.GetAccount(Address address) => GetAccount(address);

    // World World.SetAccount(Address address, Account account) => SetAccount(address, (Account)account);
}
