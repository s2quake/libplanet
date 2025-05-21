using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public sealed class BlockChainStates(Repository repository)
{
    private readonly TrieStateStore _stateStore = repository.StateStore;

    public World GetWorld(BlockHash blockHash) => new()
    {
        Trie = GetTrie(blockHash),
        StateStore = _stateStore,
    };

    public World GetWorld(HashDigest<SHA256> stateRootHash) => new()
    {
        Trie = GetTrie(stateRootHash),
        StateStore = _stateStore,
    };

    private ITrie GetTrie(BlockHash blockHash)
    {
        throw new NotImplementedException();
        // return GetTrie(repository.BlockDigests[blockHash].StateRootHash);
    }

    private ITrie GetTrie(HashDigest<SHA256> stateRootHash)
    {
        var trie = _stateStore.GetStateRoot(stateRootHash);
        if (!trie.IsCommitted)
        {
            throw new ArgumentException(
                $"Could not find state root {stateRootHash} in {nameof(TrieStateStore)}.",
                nameof(stateRootHash));
        }

        return trie;
    }
}
