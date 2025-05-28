// using Libplanet.Data;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;

// namespace Libplanet.Tests.Store;

// public sealed class StoreTracker(Libplanet.Data.Store store) : BaseTracker, Libplanet.Data.Store
// {
//     private bool _disposed = false;

//     public long AppendIndex(Guid chainId, BlockHash hash)
//     {
//         Log(nameof(AppendIndex), chainId, hash);
//         return store.AppendIndex(chainId, hash);
//     }

//     public long CountBlocks()
//     {
//         Log(nameof(CountBlocks));
//         return store.CountBlocks();
//     }

//     public long CountIndex(Guid chainId)
//     {
//         Log(nameof(CountIndex), chainId);
//         return store.CountIndex(chainId);
//     }

//     public bool DeleteBlock(BlockHash blockHash)
//     {
//         Log(nameof(DeleteBlock), blockHash);
//         return store.DeleteBlock(blockHash);
//     }

//     public bool ContainsBlock(BlockHash blockHash)
//     {
//         Log(nameof(ContainsBlock), blockHash);
//         return store.ContainsBlock(blockHash);
//     }

//     public void PutTxExecution(TxExecution txExecution)
//     {
//         Log(nameof(PutTxExecution), txExecution);
//         store.PutTxExecution(txExecution);
//     }

//     public TxExecution GetTxExecution(BlockHash blockHash, TxId txid)
//     {
//         Log(nameof(GetTxExecution), blockHash, txid);
//         return store.GetTxExecution(blockHash, txid);
//     }

//     public void BlockHashByTxId.Add(TxId txId, BlockHash blockHash)
//     {
//         Log(nameof(BlockHashByTxId.Add), txId, blockHash);
//         store.BlockHashByTxId.Add(txId, blockHash);
//     }

//     public BlockHash? BlockHashByTxId[TxId txId]
//     {
//         Log(nameof(GetFirstTxIdBlockHashIndex), txId);
//         return store.BlockHashByTxId[txId];
//     }

//     public IEnumerable<BlockHash> IterateTxIdBlockHashIndex(TxId txId)
//     {
//         Log(nameof(IterateTxIdBlockHashIndex), txId);
//         return store.IterateTxIdBlockHashIndex(txId);
//     }

//     public void BlockHashByTxId.Remove(TxId txId, BlockHash blockHash)
//     {
//         Log(nameof(BlockHashByTxId.Remove), txId, blockHash);
//         store.BlockHashByTxId.Remove(txId, blockHash);
//     }

//     public void DeleteChainId(Guid chainId)
//     {
//         Log(nameof(DeleteChainId), chainId);
//         store.DeleteChainId(chainId);
//     }

//     public Block GetBlock(BlockHash blockHash)
//     {
//         Log(nameof(GetBlock), blockHash);
//         return store.GetBlock(blockHash);
//     }

//     public long GetBlockHeight(BlockHash blockHash)
//     {
//         Log(nameof(GetBlockHeight), blockHash);
//         return store.GetBlockHeight(blockHash);
//     }

//     public BlockDigest GetBlockDigest(BlockHash blockHash)
//     {
//         Log(nameof(GetBlockDigest), blockHash);
//         return store.GetBlockDigest(blockHash);
//     }

//     public Transaction GetTransaction(TxId txid)
//     {
//         Log(nameof(GetTransaction), txid);
//         return store.GetTransaction(txid);
//     }

//     public BlockHash GetBlockHash(Guid chainId, int height)
//     {
//         Log(nameof(GetBlockHash), chainId, height);
//         return store.GetBlockHash(chainId, height);
//     }

//     public IEnumerable<BlockHash> IterateBlockHashes()
//     {
//         Log(nameof(IterateBlockHashes));
//         return store.IterateBlockHashes();
//     }

//     public IEnumerable<BlockHash> IterateIndexes(Guid chainId, int offset, int? limit)
//     {
//          Log(nameof(IterateIndexes), chainId, offset, limit);
//          return store.IterateIndexes(chainId, offset, limit);
//     }

//     public IEnumerable<Guid> ListChainIds()
//     {
//         Log(nameof(ListChainIds));
//         return store.ListChainIds();
//     }

//     public void PutBlock(Block block)
//     {
//         Log(nameof(PutBlock), block);
//         store.PutBlock(block);
//     }

//     public void PutTransaction(Transaction tx)
//     {
//         Log(nameof(PutTransaction), tx);
//         store.PutTransaction(tx);
//     }

//     public bool ContainsTransaction(TxId txId)
//     {
//         Log(nameof(ContainsTransaction), txId);
//         return store.ContainsTransaction(txId);
//     }

//     public void ForkBlockIndexes(
//         Guid sourceChainId,
//         Guid destinationChainId,
//         BlockHash branchPoint)
//     {
//         Log(nameof(ForkBlockIndexes), sourceChainId, destinationChainId, branchPoint);
//         store.ForkBlockIndexes(sourceChainId, destinationChainId, branchPoint);
//     }

//     public IEnumerable<KeyValuePair<Address, long>> ListTxNonces(Guid chainId)
//     {
//         Log(nameof(ListTxNonces), chainId);
//         return store.ListTxNonces(chainId);
//     }

//     public long GetTxNonce(Guid chainId, Address address)
//     {
//         Log(nameof(GetTxNonce), chainId, address);
//         return store.GetTxNonce(chainId, address);
//     }

//     public void IncreaseTxNonce(Guid chainId, Address address, long delta = 1)
//     {
//         Log(nameof(IncreaseTxNonce), chainId, address, delta);
//         store.IncreaseTxNonce(chainId, address, delta);
//     }

//     public void ForkTxNonces(Guid sourceChainId, Guid destinationChainId)
//     {
//         Log(nameof(ForkTxNonces), sourceChainId, destinationChainId);
//         store.ForkTxNonces(sourceChainId, destinationChainId);
//     }

//     public void PruneOutdatedChains(bool noopWithoutCanon = false)
//     {
//         Log(nameof(PruneOutdatedChains));
//         store.PruneOutdatedChains();
//     }

//     public BlockCommit GetChainBlockCommit(Guid chainId)
//     {
//         Log(nameof(GetChainBlockCommit), chainId);
//         return store.GetChainBlockCommit(chainId);
//     }

//     public void PutChainBlockCommit(Guid chainId, BlockCommit blockCmmit)
//     {
//         Log(nameof(PutChainBlockCommit), blockCmmit);
//         store.PutChainBlockCommit(chainId, blockCmmit);
//     }

//     public BlockCommit GetBlockCommit(BlockHash blockHash)
//     {
//         Log(nameof(GetBlockCommit), blockHash);
//         return store.GetBlockCommit(blockHash);
//     }

//     public void PutBlockCommit(BlockCommit commit)
//     {
//         Log(nameof(PutBlockCommit), commit);
//         store.PutBlockCommit(commit);
//     }

//     public void DeleteBlockCommit(BlockHash blockHash)
//     {
//         Log(nameof(DeleteBlockCommit), blockHash);
//         store.DeleteBlockCommit(blockHash);
//     }

//     public IEnumerable<BlockHash> GetBlockCommitHashes()
//     {
//         Log(nameof(GetBlockCommitHashes));
//         return store.GetBlockCommitHashes();
//     }

//     public IEnumerable<EvidenceId> IteratePendingEvidenceIds()
//     {
//         Log(nameof(IteratePendingEvidenceIds));
//         return store.IteratePendingEvidenceIds();
//     }

//     public EvidenceBase GetPendingEvidence(EvidenceId evidenceId)
//     {
//         Log(nameof(GetPendingEvidence));
//         return store.GetPendingEvidence(evidenceId);
//     }

//     public EvidenceBase GetCommittedEvidence(EvidenceId evidenceId)
//     {
//         Log(nameof(GetCommittedEvidence));
//         return store.GetCommittedEvidence(evidenceId);
//     }

//     public void PutPendingEvidence(EvidenceBase evidence)
//     {
//         Log(nameof(PutPendingEvidence));
//         store.PutPendingEvidence(evidence);
//     }

//     public void PutCommittedEvidence(EvidenceBase evidence)
//     {
//         Log(nameof(PutCommittedEvidence));
//         store.PutCommittedEvidence(evidence);
//     }

//     public void DeletePendingEvidence(EvidenceId evidenceId)
//     {
//         Log(nameof(DeletePendingEvidence));
//         store.DeletePendingEvidence(evidenceId);
//     }

//     public void DeleteCommittedEvidence(EvidenceId evidenceId)
//     {
//         Log(nameof(DeleteCommittedEvidence));
//         store.DeleteCommittedEvidence(evidenceId);
//     }

//     public bool ContainsPendingEvidence(EvidenceId evidenceId)
//     {
//         Log(nameof(ContainsPendingEvidence));
//         return store.ContainsPendingEvidence(evidenceId);
//     }

//     public bool ContainsCommittedEvidence(EvidenceId evidenceId)
//     {
//         Log(nameof(ContainsCommittedEvidence));
//         return store.ContainsCommittedEvidence(evidenceId);
//     }

//     public Guid? GetCanonicalChainId()
//     {
//         Log(nameof(GetCanonicalChainId));
//         return store.GetCanonicalChainId();
//     }

//     public void SetCanonicalChainId(Guid chainId)
//     {
//         Log(nameof(SetCanonicalChainId), chainId);
//         store.SetCanonicalChainId(chainId);
//     }

//     public void Dispose()
//     {
//         if (!_disposed)
//         {
//             store.Dispose();
//             _disposed = true;
//         }
//     }
// }
