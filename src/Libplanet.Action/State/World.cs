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
    public required ITrie Trie { get; init; }

    public required IStateStore StateStore { get; init; }

    public Address Signer { get; init; }

    public int Version { get; init; }

    public ImmutableDictionary<Address, Account> Delta { get; private init; }
        = ImmutableDictionary<Address, Account>.Empty;

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
}
