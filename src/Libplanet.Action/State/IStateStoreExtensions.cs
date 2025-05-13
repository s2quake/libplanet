using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Store;
using Libplanet.Types;

namespace Libplanet.Action.State;

internal static class IStateStoreExtensions
{
    public static World GetWorld(this TrieStateStore @this, HashDigest<SHA256> stateRootHash) => new()
    {
        Trie = @this.GetStateRoot(stateRootHash),
        StateStore = @this,
    };

    public static World CommitWorld(this TrieStateStore stateStore, World world)
    {
        var trie = world.Trie;
        foreach (var (name, account) in world.Delta)
        {
            var accountTrie = stateStore.Commit(account.Trie);
            var key = name;
            var value = new Binary(accountTrie.Hash.Bytes);
            trie = trie.Set(key, value);
        }

        return new World { Trie = stateStore.Commit(trie), StateStore = stateStore };
    }
}
