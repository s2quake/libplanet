using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain
{
    public partial class BlockChain
    {
        /// <summary>
        /// Gets the current world state in the <see cref="BlockChain"/>.
        /// </summary>
        /// <returns>The current world state.</returns>
        public World GetWorldState() => GetWorld(Tip.BlockHash);

        /// <inheritdoc cref="IBlockChainStates.GetWorld(BlockHash)" />
        public World GetWorld(BlockHash offset)
            => _blockChainStates.GetWorld(offset);

        /// <inheritdoc cref="IBlockChainStates.GetWorld(HashDigest{SHA256})" />
        public World GetWorld(HashDigest<SHA256> stateRootHash)
            => _blockChainStates.GetWorld(stateRootHash);

        /// <summary>
        /// Gets the next world state in the <see cref="BlockChain"/>.
        /// </summary>
        /// <returns>The next world state.  If it does not exist, returns null.</returns>
        public World GetNextWorld()
        {
            if (GetNextStateRootHash() is { } nsrh)
            {
                var trie = StateStore.GetStateRoot(nsrh);
                return trie.IsCommitted
                    ? new World { Trie = trie, StateStore = StateStore }
                    : throw new InvalidOperationException(
                        $"Could not find state root {nsrh} in {nameof(StateStore)} for " +
                        $"the current tip.");
            }
            else
            {
                return null!;
            }
        }
    }
}
