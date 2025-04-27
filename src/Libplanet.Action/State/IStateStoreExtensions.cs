using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Store;

namespace Libplanet.Action.State;

internal static class IStateStoreExtensions
{
    public static IWorld GetWorld(this IStateStore stateStore, HashDigest<SHA256> stateRootHash)
    {
        return new World(
            new WorldBaseState(stateStore.GetStateRoot(stateRootHash), stateStore));
    }

    public static IWorld CommitWorld(this IStateStore stateStore, IWorld world)
    {
        var worldTrie = world.Trie;
        foreach (var account in world.Delta.Accounts)
        {
            var accountTrie = stateStore.Commit(account.Value.Trie);
            worldTrie = worldTrie.Set(
                KeyConverters.ToStateKey(account.Key),
                new Binary(accountTrie.Hash.Bytes));
        }

        return new World(
            new WorldBaseState(stateStore.Commit(worldTrie), stateStore));
    }
}
