using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Store;

namespace Libplanet.Action.State;

internal static class IStateStoreExtensions
{
    public static IWorld GetWorld(this IStateStore stateStore, HashDigest<SHA256> stateRootHash)
    {
        return new World(stateStore.GetStateRoot(stateRootHash), stateStore);
    }

    public static IWorld CommitWorld(this IStateStore stateStore, IWorld world)
    {
        var worldTrie = world.Trie;
        foreach (var (address, account) in world.Delta)
        {
            var accountTrie = stateStore.Commit(account.Trie);
            worldTrie = worldTrie.Set(
                KeyConverters.ToStateKey(address),
                new Binary(accountTrie.Hash.Bytes));
        }

        return new World(stateStore.Commit(worldTrie), stateStore);
    }
}
