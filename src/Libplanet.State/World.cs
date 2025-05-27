using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Data.Structures;
using Libplanet.Types;
using Libplanet.Types.Crypto;

namespace Libplanet.State;

public sealed record class World(ITrie Trie, StateIndex Statestore)
{
    public World(StateIndex stateStore)
        : this(stateStore.GetTrie(default), stateStore)
    {
    }

    public World()
        : this(new StateIndex())
    {
    }

    public World(StateIndex stateStore, HashDigest<SHA256> stateRootHash)
        : this(stateStore.GetTrie(stateRootHash), stateStore)
    {
    }

    public HashDigest<SHA256> Hash => Trie.Hash;

    public Address Signer { get; init; }

    public ImmutableDictionary<string, Account> Delta { get; private init; }
        = ImmutableDictionary<string, Account>.Empty;

    internal ITrie Trie { get; } = Trie;

    internal StateIndex StateStore { get; init; } = Statestore;

    public Account GetAccount(string name)
    {
        if (Delta.TryGetValue(name, out var account))
        {
            return account;
        }

        if (Trie.TryGetValue(name, out var value) && value is ImmutableArray<byte> binary)
        {
            return new Account(StateStore.GetTrie(new HashDigest<SHA256>(binary)));
        }

        return new Account(StateStore.GetTrie(default));
    }

    public World SetAccount(string name, Account account) => this with
    {
        Delta = Delta.SetItem(name, account),
    };

    public World Commit()
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
