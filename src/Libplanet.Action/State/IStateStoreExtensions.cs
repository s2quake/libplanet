using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Store;

namespace Libplanet.Action.State
{
    internal static class IStateStoreExtensions
    {
        /// <summary>
        /// Retrieves the <see cref="IWorld"/> associated with
        /// given <paramref name="stateRootHash"/>.
        /// </summary>
        /// <param name="stateStore">The <see cref="IStateStore"/> to retrieve
        /// an <see cref="IWorld"/> from.</param>
        /// <param name="stateRootHash">The state root hash of the <see cref="IWorld"/>
        /// to retrieve.</param>
        /// <returns>The <see cref="IWorld"/> associated with
        /// given <paramref name="stateRootHash"/>.</returns>
        internal static IWorld GetWorld(
            this IStateStore stateStore,
            HashDigest<SHA256> stateRootHash)
        {
            return new World(
                new WorldBaseState(stateStore.GetStateRoot(stateRootHash), stateStore));
        }

        /// <summary>
        /// Commits given <paramref name="world"/> to given <paramref name="stateStore"/>.
        /// </summary>
        /// <param name="stateStore">The <see cref="IStateStore"/> to commit
        /// <paramref name="world"/> to.</param>
        /// <param name="world">The <see cref="IWorld"/> to commit.</param>
        /// <returns>The committed <see cref="IWorld"/>.</returns>
        internal static IWorld CommitWorld(this IStateStore stateStore, IWorld world)
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
}
