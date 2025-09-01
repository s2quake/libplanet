using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.State.Structures;
using Libplanet.Types;

namespace Libplanet.State;

public sealed record class World(Trie Trie, StateIndex StateIndex)
{
    public World(StateIndex stateIndex)
        : this(stateIndex.GetTrie(default), stateIndex)
    {
    }

    public World()
        : this(new StateIndex(new MemoryDatabase()))
    {
    }

    public World(StateIndex stateIndex, HashDigest<SHA256> stateRootHash)
        : this(stateIndex.GetTrie(stateRootHash), stateIndex)
    {
    }

    public HashDigest<SHA256> Hash => Trie.Hash;

    public Address Signer { get; init; }

    public ImmutableDictionary<string, Account> Delta { get; private init; }
        = ImmutableDictionary<string, Account>.Empty;

    internal Trie Trie { get; } = Trie;

    internal StateIndex StateIndex { get; init; } = StateIndex;

    public Account GetAccount(string name)
    {
        if (Delta.TryGetValue(name, out var account))
        {
            return account;
        }

        if (Trie.TryGetValue(name, out var value) && value is ImmutableArray<byte> binary)
        {
            return new Account(StateIndex.GetTrie(new HashDigest<SHA256>(binary)));
        }

        return new Account();
    }

    public World SetAccount(string name, Account account) => this with
    {
        Delta = Delta.SetItem(name, account),
    };

    public World Commit()
    {
        if (Delta.IsEmpty)
        {
            return this;
        }

        var trie = Trie;
        foreach (var (name, account) in Delta)
        {
            var accountTrie = StateIndex.Commit(account.Trie);
            var key = name;
            var value = accountTrie.Hash.Bytes;
            trie = trie.Set(key, value);
        }

        return new World(StateIndex.Commit(trie), StateIndex);
    }
}
