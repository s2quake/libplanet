using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Store;

namespace Libplanet.Action.State;

internal static class IStateStoreExtensions
{
    public static IWorld GetWorld(this IStateStore @this, HashDigest<SHA256> stateRootHash)
        => new World(@this.GetStateRoot(stateRootHash), @this);

    public static IWorld CommitWorld(this IStateStore stateStore, IWorld world)
    {
        var trie = world.Trie;
        foreach (var (address, account) in world.Delta)
        {
            var accountTrie = stateStore.Commit(account.Trie);
            var key = KeyConverters.ToStateKey(address);
            var value = new Binary(accountTrie.Hash.Bytes);
            trie = trie.Set(key, value);
        }

        return new World(stateStore.Commit(trie), stateStore);
    }
}
