using System.Security.Cryptography;
using Libplanet.Store;
using Libplanet.Store.DataStructures;
using Libplanet.Types;
using Libplanet.Types.Crypto;

namespace Libplanet.Action.State;

public sealed record class World(ITrie Trie, StateStore Statestore)
{
    public World(StateStore stateStore)
        : this(stateStore.GetStateRoot(default), stateStore)
    {
    }

    public World()
        : this(new StateStore())
    {
    }

    public World(StateStore stateStore, HashDigest<SHA256> stateRootHash)
        : this(stateStore.GetStateRoot(stateRootHash), stateStore)
    {
    }

    public HashDigest<SHA256> Hash => Trie.Hash;

    public Address Signer { get; init; }

    public ImmutableDictionary<string, Account> Delta { get; private init; }
        = ImmutableDictionary<string, Account>.Empty;

    internal ITrie Trie { get; } = Trie;

    internal StateStore StateStore { get; init; } = Statestore;

    public Account GetAccount(string name)
    {
        if (Delta.TryGetValue(name, out var account))
        {
            return account;
        }

        if (Trie.TryGetValue(name, out var value) && value is ImmutableArray<byte> binary)
        {
            return new Account(StateStore.GetStateRoot(new HashDigest<SHA256>(binary)));
        }

        return new Account(StateStore.GetStateRoot(default));
    }

    public World SetAccount(string name, Account account) => this with
    {
        Delta = Delta.SetItem(name, account),
    };

    internal World Commit()
    {
        var trie = Trie;
        foreach (var (name, account) in Delta)
        {
            var accountTrie = StateStore.Commit(account.Trie);
            var key = name;
            var value = accountTrie.Hash.Bytes;
            trie = trie.Set(key, value);
        }

        return new World(StateStore.Commit(trie), StateStore);
    }
}
