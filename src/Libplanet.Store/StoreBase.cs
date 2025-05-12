// using System.Security.Cryptography;
// using Bencodex.Types;
// using Libplanet.Serialization;
// using Libplanet.Types;
// using Libplanet.Types.Assets;
// using Libplanet.Types.Blocks;
// using Libplanet.Types.Crypto;
// using Libplanet.Types.Evidence;
// using Libplanet.Types.Tx;
// using Serilog;
// using FAV = Libplanet.Types.Assets.FungibleAssetValue;

// namespace Libplanet.Store;

// public abstract class StoreBase : Libplanet.Store.Store
// {
//     public abstract IEnumerable<Guid> ListChainIds();

//     public abstract Guid GetCanonicalChainId();

//     public abstract void SetCanonicalChainId(Guid chainId);

//     public abstract int CountIndex(Guid chainId);

//     public abstract IEnumerable<BlockHash> IterateIndexes(Guid chainId, int offset, int? limit);

//     public abstract BlockHash GetBlockHash(Guid chainId, int height);

//     public abstract int AppendIndex(Guid chainId, BlockHash hash);

//     public abstract void ForkBlockIndexes(
//         Guid sourceChainId,
//         Guid destinationChainId,
//         BlockHash branchpoint);

//     public abstract Transaction GetTransaction(TxId txId);

//     public abstract void PutTransaction(Transaction tx);

//     public abstract IEnumerable<BlockHash> IterateBlockHashes();

//     public Block GetBlock(BlockHash blockHash)
//         => GetBlockDigest(blockHash).ToBlock(GetTransaction, GetCommittedEvidence);

//     public int GetBlockHeight(BlockHash blockHash)
//     {
//         return GetBlockDigest(blockHash).Height;
//     }

//     public abstract BlockDigest GetBlockDigest(BlockHash blockHash);

//     public abstract void PutBlock(Block block);

//     public abstract bool DeleteBlock(BlockHash blockHash);

//     public abstract bool ContainsBlock(BlockHash blockHash);

//     public abstract void PutTxExecution(TxExecution txExecution);

//     public abstract TxExecution GetTxExecution(BlockHash blockHash, TxId txId);

//     public abstract void BlockHashByTxId.Add(TxId txId, BlockHash blockHash);

//     public BlockHash BlockHashByTxId[TxId txId]
//     {
//         var item = IterateTxIdBlockHashIndex(txId).FirstOrDefault();
//         if (item == default)
//         {
//             throw new KeyNotFoundException(
//                 $"The transaction ID {txId} does not exist in the index.");
//         }

//         return item;
//     }

//     public abstract IEnumerable<BlockHash> IterateTxIdBlockHashIndex(TxId txId);

//     public abstract void BlockHashByTxId.Remove(TxId txId, BlockHash blockHash);

//     public abstract IEnumerable<KeyValuePair<Address, long>> ListTxNonces(Guid chainId);

//     public abstract long GetTxNonce(Guid chainId, Address address);

//     public abstract void IncreaseTxNonce(Guid chainId, Address signer, long delta = 1);

//     public virtual long CountBlocks()
//     {
//         return IterateBlockHashes().LongCount();
//     }

//     public abstract bool ContainsTransaction(TxId txId);

//     public abstract void DeleteChainId(Guid chainId);

//     public abstract void Dispose();

//     public abstract void ForkTxNonces(Guid sourceChainId, Guid destinationChainId);

//     public abstract void PruneOutdatedChains(bool noopWithoutCanon = false);

//     public abstract BlockCommit GetChainBlockCommit(Guid chainId);

//     public abstract void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit);

//     public abstract BlockCommit GetBlockCommit(BlockHash blockHash);

//     public abstract void PutBlockCommit(BlockCommit blockCommit);

//     public abstract void DeleteBlockCommit(BlockHash blockHash);

//     public abstract IEnumerable<BlockHash> GetBlockCommitHashes();

//     public abstract HashDigest<SHA256> GetNextStateRootHash(BlockHash blockHash);

//     public abstract void PutNextStateRootHash(
//         BlockHash blockHash, HashDigest<SHA256> nextStateRootHash);

//     public abstract void DeleteNextStateRootHash(BlockHash blockHash);

//     public abstract IEnumerable<EvidenceId> IteratePendingEvidenceIds();

//     public abstract EvidenceBase GetPendingEvidence(EvidenceId evidenceId);

//     public abstract EvidenceBase GetCommittedEvidence(EvidenceId evidenceId);

//     public abstract void PutPendingEvidence(EvidenceBase evidence);

//     public abstract void PutCommittedEvidence(EvidenceBase evidence);

//     public abstract void DeletePendingEvidence(EvidenceId evidenceId);

//     public abstract void DeleteCommittedEvidence(EvidenceId evidenceId);

//     public abstract bool ContainsPendingEvidence(EvidenceId evidenceId);

//     public abstract bool ContainsCommittedEvidence(EvidenceId evidenceId);

//     protected static IValue SerializeTxExecution(TxExecution txExecution)
//     {
//         return ModelSerializer.Serialize(txExecution);
//     }

//     protected static TxExecution? DeserializeTxExecution(
//         BlockHash blockHash,
//         TxId txId,
//         IValue decoded,
//         ILogger logger)
//     {
//         if (!(decoded is Bencodex.Types.Dictionary d))
//         {
//             const string msg = nameof(TxExecution) +
//                 " must be serialized as a Bencodex dictionary, not {ActualValue}";
//             logger?.Error(msg, decoded.Inspect());
//             return null;
//         }

//         try
//         {
//             return ModelSerializer.Deserialize<TxExecution>(d);
//         }
//         catch (Exception e)
//         {
//             const string msg =
//                 "Uncaught exception during deserializing a " + nameof(TxExecution);
//             logger?.Error(e, msg);
//             return null;
//         }
//     }
// }
