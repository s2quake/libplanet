// using System.Collections.Concurrent;
// using System.Collections.Specialized;
// using System.Web;
// using ImmutableTrie;
// using Libplanet.Store.Trie;
// using Libplanet.Types.Blocks;
// using Libplanet.Types.Crypto;
// using Libplanet.Types.Evidence;
// using Libplanet.Types.Tx;

// namespace Libplanet.Store;

// public sealed class MemoryStore : Libplanet.Store.Store
// {
//     private readonly ConcurrentDictionary<Guid, ImmutableTrieList<BlockHash>> _indexes = new();
//     private readonly ConcurrentDictionary<BlockHash, BlockDigest> _blocks = new();
//     private readonly ConcurrentDictionary<TxId, Transaction> _txs = new();
//     private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Address, long>> _txNonces =
//         new();

//     private readonly ConcurrentDictionary<(BlockHash, TxId), TxExecution> _txExecutions = new();
//     private readonly ConcurrentDictionary<TxId, ImmutableHashSet<BlockHash>> _txBlockIndexes =
//         new();

//     private readonly ConcurrentDictionary<BlockHash, BlockCommit> _blockCommits = new();
//     private readonly ConcurrentDictionary<Guid, BlockCommit> _chainCommits = new();
//     private readonly ConcurrentDictionary<EvidenceId, EvidenceBase> _pendingEvidence = new();
//     private readonly ConcurrentDictionary<EvidenceId, EvidenceBase> _committedEvidence = new();

//     private Guid? _canonicalChainId;

//     void IDisposable.Dispose()
//     {
//         // Do nothing.
//     }

//     IEnumerable<Guid> Libplanet.Store.Store.ListChainIds() => _indexes.Keys;

//     void Libplanet.Store.Store.DeleteChainId(Guid chainId)
//     {
//         _indexes.TryRemove(chainId, out _);
//         _txNonces.TryRemove(chainId, out _);
//     }

//     Guid Libplanet.Store.Store.GetCanonicalChainId() => _canonicalChainId ?? Guid.Empty;

//     void Libplanet.Store.Store.SetCanonicalChainId(Guid chainId) => _canonicalChainId = chainId;

//     int Libplanet.Store.Store.CountIndex(Guid chainId) =>
//         _indexes.TryGetValue(chainId, out ImmutableTrieList<BlockHash>? index)
//         ? index.Count
//         : 0;

//     IEnumerable<BlockHash> Libplanet.Store.Store.IterateIndexes(Guid chainId, int offset, int? limit)
//     {
//         if (_indexes.TryGetValue(chainId, out var list))
//         {
//             IEnumerable<BlockHash> index = list.Skip(offset);
//             return limit is { } l ? index.Take(l) : index;
//         }

//         return [];
//     }

//     BlockHash Libplanet.Store.Store.GetBlockHash(Guid chainId, int height)
//     {
//         if (_indexes.TryGetValue(chainId, out var list))
//         {
//             if (height < 0)
//             {
//                 height += list.Count;
//             }

//             if (height < list.Count && height >= 0)
//             {
//                 return list[(int)height];
//             }
//         }

//         throw new ArgumentOutOfRangeException(
//             nameof(height),
//             $"The height {height} is out of range for the chain ID {chainId}.");
//     }

//     int Libplanet.Store.Store.AppendIndex(Guid chainId, BlockHash hash)
//     {
//         ImmutableTrieList<BlockHash> list = _indexes.AddOrUpdate(
//             chainId,
//             _ => ImmutableTrieList.Create(hash),
//             (_, list) => list.Add(hash));
//         _txNonces.GetOrAdd(chainId, _ => new ConcurrentDictionary<Address, long>());

//         return list.Count - 1;
//     }

//     public void ForkBlockIndexes(
//         Guid sourceChainId,
//         Guid destinationChainId,
//         BlockHash branchpoint)
//     {
//         if (_indexes.TryGetValue(sourceChainId, out ImmutableTrieList<BlockHash>? source))
//         {
//             int bpIndex = source.FindIndex(branchpoint.Equals);
//             _indexes[destinationChainId] = source.GetRange(0, bpIndex + 1);
//         }
//     }

//     Transaction? Libplanet.Store.Store.GetTransaction(TxId txId) =>
//         _txs.TryGetValue(txId, out Transaction? untyped) && untyped is Transaction tx
//             ? tx
//             : null;

//     void Libplanet.Store.Store.PutTransaction(Transaction tx) => _txs[tx.Id] = tx;

//     IEnumerable<BlockHash> Libplanet.Store.Store.IterateBlockHashes() => _blocks.Keys;

//     Block Libplanet.Store.Store.GetBlock(BlockHash blockHash)
//     {
//         if (!_blocks.TryGetValue(blockHash, out var blockDigest))
//         {
//             throw new KeyNotFoundException(
//                 $"The block hash {blockHash} is not found in the store.");
//         }

//         return blockDigest.ToBlock(txId => _txs[txId], evId => _committedEvidence[evId]);
//     }

//     int Libplanet.Store.Store.GetBlockHeight(BlockHash blockHash)
//     {
//         if (!_blocks.TryGetValue(blockHash, out var digest))
//         {
//             throw new KeyNotFoundException($"The block hash {blockHash} is not found in the store.");
//         }

//         return digest.Height;
//     }

//     BlockDigest Libplanet.Store.Store.GetBlockDigest(BlockHash blockHash)
//     {
//         if (!_blocks.TryGetValue(blockHash, out var blockDigest))
//         {
//             throw new KeyNotFoundException(
//                 $"The block hash {blockHash} is not found in the store.");
//         }

//         return blockDigest;
//     }

//     void Libplanet.Store.Store.PutBlock(Block block)
//     {
//         IReadOnlyList<Transaction> txs = block.Transactions;
//         foreach (Transaction tx in txs)
//         {
//             _txs[tx.Id] = tx;
//         }

//         var evidence = block.Evidences;
//         foreach (var ev in evidence)
//         {
//             _committedEvidence[ev.Id] = ev;
//         }

//         _blocks[block.BlockHash] = new BlockDigest
//         {
//             Header = block.Header,
//             StateRootHash = block.StateRootHash,
//             Signature = block.Signature,
//             TxIds = [.. txs.Select(tx => tx.Id)],
//             EvidenceIds = [.. evidence.Select(ev => ev.Id)],
//             BlockHash = block.BlockHash,
//         };
//     }

//     bool Libplanet.Store.Store.DeleteBlock(BlockHash blockHash) => _blocks.TryRemove(blockHash, out _);

//     bool Libplanet.Store.Store.ContainsBlock(BlockHash blockHash) => _blocks.ContainsKey(blockHash);

//     void Libplanet.Store.Store.PutTxExecution(TxExecution txExecution) =>
//         _txExecutions[(txExecution.BlockHash, txExecution.TxId)] = txExecution;

//     TxExecution Libplanet.Store.Store.GetTxExecution(BlockHash blockHash, TxId txId) =>
//         _txExecutions.TryGetValue((blockHash, txId), out TxExecution? e)
//             ? e
//             : throw new KeyNotFoundException(
//                 $"The transaction ID {txId} is not found in the block {blockHash}.");

//     void Libplanet.Store.Store.BlockHashByTxId.Add(TxId txId, BlockHash blockHash) =>
//         _txBlockIndexes.AddOrUpdate(
//             txId,
//             _ => ImmutableHashSet.Create(blockHash),
//             (_, set) => set.Add(blockHash));

//     BlockHash Libplanet.Store.Store.BlockHashByTxId[TxId txId] =>
//         _txBlockIndexes.TryGetValue(txId, out ImmutableHashSet<BlockHash>? set) && set.Any()
//             ? set.First()
//             : throw new KeyNotFoundException(
//                 $"The transaction ID {txId} is not found in the store.");

//     IEnumerable<BlockHash> Libplanet.Store.Store.IterateTxIdBlockHashIndex(TxId txId) =>
//         _txBlockIndexes.TryGetValue(txId, out ImmutableHashSet<BlockHash>? set)
//             ? set
//             : Enumerable.Empty<BlockHash>();

//     void Libplanet.Store.Store.BlockHashByTxId.Remove(TxId txId, BlockHash blockHash)
//     {
//         while (_txBlockIndexes.TryGetValue(txId, out ImmutableHashSet<BlockHash>? set) &&
//                set.Contains(blockHash))
//         {
//             var removed = set.Remove(blockHash);
//             _txBlockIndexes.TryUpdate(txId, removed, set);
//         }
//     }

//     IEnumerable<KeyValuePair<Address, long>> Libplanet.Store.Store.ListTxNonces(Guid chainId) =>
//         _txNonces.TryGetValue(chainId, out ConcurrentDictionary<Address, long>? dict)
//             ? dict
//             : Enumerable.Empty<KeyValuePair<Address, long>>();

//     long Libplanet.Store.Store.GetTxNonce(Guid chainId, Address address) =>
//         _txNonces.TryGetValue(chainId, out ConcurrentDictionary<Address, long>? dict) &&
//         dict.TryGetValue(address, out long nonce)
//             ? nonce
//             : 0;

//     void Libplanet.Store.Store.IncreaseTxNonce(Guid chainId, Address signer, long delta)
//     {
//         ConcurrentDictionary<Address, long> dict =
//             _txNonces.GetOrAdd(chainId, _ => new ConcurrentDictionary<Address, long>());
//         dict.AddOrUpdate(signer, _ => delta, (_, nonce) => nonce + delta);
//     }

//     bool Libplanet.Store.Store.ContainsTransaction(TxId txId) => _txs.ContainsKey(txId);

//     long Libplanet.Store.Store.CountBlocks() => _blocks.Count;

//     void Libplanet.Store.Store.ForkTxNonces(Guid sourceChainId, Guid destinationChainId)
//     {
//         if (_txNonces.TryGetValue(sourceChainId, out ConcurrentDictionary<Address, long>? dict))
//         {
//             _txNonces[destinationChainId] = new ConcurrentDictionary<Address, long>(dict);
//         }
//     }

//     void Libplanet.Store.Store.PruneOutdatedChains(bool noopWithoutCanon)
//     {
//         if (!(_canonicalChainId is { } ccid))
//         {
//             if (noopWithoutCanon)
//             {
//                 return;
//             }

//             throw new InvalidOperationException("Canonical chain ID is not assigned.");
//         }

//         foreach (Guid id in _indexes.Keys.Where(id => !id.Equals(ccid)))
//         {
//             ((Libplanet.Store.Store)this).DeleteChainId(id);
//         }
//     }

//     public BlockCommit GetChainBlockCommit(Guid chainId) =>
//         _chainCommits.TryGetValue(chainId, out var commit)
//             ? commit
//             : default;

//     public void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit) =>
//         _chainCommits[chainId] = blockCommit;

//     public BlockCommit GetBlockCommit(BlockHash blockHash) =>
//         _blockCommits.TryGetValue(blockHash, out var commit)
//             ? commit
//             : default;

//     public void PutBlockCommit(BlockCommit blockCommit) =>
//         _blockCommits[blockCommit.BlockHash] = blockCommit;

//     public void DeleteBlockCommit(BlockHash blockHash) =>
//         _blockCommits.TryRemove(blockHash, out _);

//     public IEnumerable<BlockHash> GetBlockCommitHashes()
//         => _blockCommits.Keys;

//     public IEnumerable<EvidenceId> IteratePendingEvidenceIds()
//         => _pendingEvidence.Keys;

//     public EvidenceBase? GetPendingEvidence(EvidenceId evidenceId)
//         => _pendingEvidence.TryGetValue(evidenceId, out var evidence)
//         ? evidence
//         : null;

//     public void PutPendingEvidence(EvidenceBase evidence)
//         => _pendingEvidence[evidence.Id] = evidence;

//     public void DeletePendingEvidence(EvidenceId evidenceId)
//         => _pendingEvidence.TryRemove(evidenceId, out _);

//     public bool ContainsPendingEvidence(EvidenceId evidenceId)
//         => _pendingEvidence.ContainsKey(evidenceId);

//     public EvidenceBase? GetCommittedEvidence(EvidenceId evidenceId)
//         => _committedEvidence.TryGetValue(evidenceId, out var evidence)
//         ? evidence
//         : null;

//     public void PutCommittedEvidence(EvidenceBase evidence)
//         => _committedEvidence[evidence.Id] = evidence;

//     public void DeleteCommittedEvidence(EvidenceId evidenceId)
//         => _committedEvidence.TryRemove(evidenceId, out _);

//     public bool ContainsCommittedEvidence(EvidenceId evidenceId)
//         => _committedEvidence.ContainsKey(evidenceId);

//     [StoreLoader("memory")]
//     private static (Libplanet.Store.Store Store, TrieStateStore StateStore) Loader(Uri storeUri)
//     {
//         NameValueCollection query = HttpUtility.ParseQueryString(storeUri.Query);
//         var store = new Libplanet.Store.Store(new MemoryDatabase());
//         var stateStore = new TrieStateStore();
//         return (store, stateStore);
//     }
// }
