using System.Security.Cryptography;
using Libplanet.Store;
using Libplanet.Store.DataStructures;
using Libplanet.Types;
using Libplanet.Types.Crypto;

namespace Libplanet.Action.State;

public sealed record class World
{
    public required ITrie Trie { get; init; }

    public required TrieStateStore StateStore { get; init; }

    public Address Signer { get; init; }

    public ImmutableDictionary<string, Account> Delta { get; private init; }
        = ImmutableDictionary<string, Account>.Empty;

    public static World Create() => Create(new TrieStateStore());

    public static World Create(TrieStateStore stateStore) => Create(stateStore.GetStateRoot(default), stateStore);

    public static World Create(ITrie trie, TrieStateStore stateStore) => new()
    {
        Trie = trie,
        StateStore = stateStore,
    };

    public static World Create(HashDigest<SHA256> stateRootHash, TrieStateStore stateStore) => new()
    {
        Trie = stateStore.GetStateRoot(stateRootHash),
        StateStore = stateStore,
    };

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
}
