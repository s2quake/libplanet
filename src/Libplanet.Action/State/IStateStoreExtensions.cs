using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Types;
using Libplanet.Store;

namespace Libplanet.Action.State;

internal static class IStateStoreExtensions
{
    public static World GetWorld(this IStateStore @this, HashDigest<SHA256> stateRootHash) => new()
    {
        Trie = @this.GetStateRoot(stateRootHash),
        StateStore = @this,
    };

    public static World CommitWorld(this IStateStore stateStore, World world)
    {
        var trie = world.Trie;
        foreach (var (address, account) in world.Delta)
        {
            var accountTrie = stateStore.Commit(account.Trie);
            var key = KeyConverters.ToStateKey(address);
            var value = new Binary(accountTrie.Hash.Bytes);
            trie = trie.Set(key, value);
        }

        return new World { Trie = stateStore.Commit(trie), StateStore = stateStore };
    }
}
