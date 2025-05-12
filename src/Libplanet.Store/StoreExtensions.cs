using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Store
{
    /// <summary>
    /// Convenient extension methods for <see cref="Libplanet.Store.Store"/>.
    /// </summary>
    public static class StoreExtensions
    {
        /// <summary>
        /// Makes a store, <paramref name="to"/>, logically (but not necessarily physically)
        /// identical to another store, <paramref name="from"/>.  As this copies the contents
        /// of the store, instead of its physicall data, this can be used for migrating
        /// between two different types of <see cref="Libplanet.Store.Store"/> implementations.
        /// </summary>
        /// <param name="from">The store containing the source contents.</param>
        /// <param name="to">The store to contain the copied contents. Expected to be empty.</param>
        /// <exception cref="ArgumentException">Thrown when the store passed through
        /// <paramref name="to"/> is not empty.</exception>
        public static void Copy(this Libplanet.Store.Store from, Libplanet.Store.Store to)
        {
            // TODO: take a IProgress<> so that a caller can be aware the progress of cloning.
            if (to.ListChainIds().Any())
            {
                throw new ArgumentException("The destination store has to be empty.", nameof(to));
            }

            var fromBlocks = from.Blocks;
            var toBlocks = to.Blocks;
            foreach (Guid chainId in from.ListChainIds().ToArray())
            {
                foreach (BlockHash blockHash in from.IterateIndexes(chainId))
                {
                    var block = fromBlocks[blockHash];
                    toBlocks.Add(block);
                    to.AppendIndex(chainId, blockHash);
                }

                foreach (KeyValuePair<Address, long> kv in from.GetNonceCollection(chainId))
                {
                    to.GetNonceCollection(chainId).Increase(kv.Key, kv.Value);
                }
            }

            if (from.ChainId is Guid canonId)
            {
                to.ChainId = canonId;
            }
        }

        /// <summary>
        /// Gets the <see cref="Block.StateRootHash"/> of the given <paramref name="blockHash"/>.
        /// </summary>
        /// <param name="store">The store that blocks are stored.</param>
        /// <param name="blockHash">The hash of the block to get the state root hash of.
        /// This can be <see langword="null"/>.</param>
        /// <returns>The state root hash of the block associated with <paramref name="blockHash"/>
        /// if found or <see langword="null"/> if <paramref name="blockHash"/> is itself
        /// <see langword="null"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="blockHash"/> is
        /// not <see langword="null"/> but the corresponding block is not found in store.
        /// </exception>
        public static HashDigest<SHA256> GetStateRootHash(this Libplanet.Store.Store store, BlockHash blockHash)
            => store.BlockDigests[blockHash].StateRootHash;
    }
}
