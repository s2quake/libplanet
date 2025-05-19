using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    public World GetWorld() => GetWorld(Tip.BlockHash);

    public World GetWorld(BlockHash blockHash) => _blockChainStates.GetWorld(blockHash);

    public World GetWorld(HashDigest<SHA256> stateRootHash) => _blockChainStates.GetWorld(stateRootHash);

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
