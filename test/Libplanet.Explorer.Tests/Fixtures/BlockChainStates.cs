namespace Libplanet.Explorer.Tests.Fixtures;

// public class BlockChainStates : IBlockChainStates
// {
//     private TrieStateStore _stateStore;

//     private Dictionary<BlockHash, HashDigest<SHA256>> _map;

//     public BlockChainStates()
//     {
//         _stateStore = new TrieStateStore();
//         _map = new Dictionary<BlockHash, HashDigest<SHA256>>();
//     }

//     public TrieStateStore StateStore => _stateStore;

//     public void AttachBlockHashToStateRootHash(
//         BlockHash blockHash,
//         HashDigest<SHA256> stateRootHash)
//     {
//         if (_map.ContainsKey(blockHash))
//         {
//             throw new ArgumentException(
//                 $"Already contains block hash {blockHash} associated to " +
//                 $"state root hash {_map[blockHash]}.",
//                 nameof(blockHash));
//         }
//         else if (!_stateStore.GetStateRoot(stateRootHash).IsCommitted)
//         {
//             throw new ArgumentException(
//                 $"No state root for given state root hash {stateRootHash} found.",
//                 nameof(stateRootHash));
//         }

//         _map[blockHash] = stateRootHash;
//     }

//     /// <inheritdoc cref="IBlockChainStates.GetWorldState(BlockHash?)"/>
//     public World GetWorld(BlockHash offset)
//     {
//         if (_map.ContainsKey(offset))
//         {
//             return GetWorld(_map[offset]);
//         }
//         else
//         {
//             throw new ArgumentException(
//                 $"No state root associated with given block hash {offset} found.",
//                 nameof(offset));
//         }
//     }

//     /// <inheritdoc cref="IBlockChainStates.GetWorldState(HashDigest{SHA256}?)"/>
//     public World GetWorld(HashDigest<SHA256> stateRootHash)
//     {
//         ITrie trie = _stateStore.GetStateRoot(stateRootHash);
//         return trie.IsCommitted
//             ? new World { Trie = trie, StateStore = _stateStore }
//             : throw new ArgumentException(
//                 $"Could not find state root {stateRootHash} in {nameof(TrieStateStore)}.",
//                 nameof(stateRootHash));
//     }
// }
