using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Data.Structures;
using Libplanet.Types;

namespace Libplanet.State;

public sealed record class World(ITrie Trie, StateIndex States)
{
    public World(StateIndex states)
        : this(states.GetTrie(default), states)
    {
    }

    public World()
        : this(new StateIndex())
    {
    }

    public World(StateIndex states, HashDigest<SHA256> stateRootHash)
        : this(states.GetTrie(stateRootHash), states)
    {
    }

    public HashDigest<SHA256> Hash => Trie.Hash;

    public Address Signer { get; init; }

    public ImmutableDictionary<string, Account> Delta { get; private init; }
        = ImmutableDictionary<string, Account>.Empty;

    internal ITrie Trie { get; } = Trie;

    internal StateIndex States { get; init; } = States;

    public Account GetAccount(string name)
    {
        if (Delta.TryGetValue(name, out var account))
        {
            return account;
        }

        if (Trie.TryGetValue(name, out var value) && value is ImmutableArray<byte> binary)
        {
            return new Account(States.GetTrie(new HashDigest<SHA256>(binary)));
        }

        return new Account();
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
            var accountTrie = States.Commit(account.Trie);
            var key = name;
            var value = accountTrie.Hash.Bytes;
            trie = trie.Set(key, value);
        }

        return new World(States.Commit(trie), States);
    }
}
