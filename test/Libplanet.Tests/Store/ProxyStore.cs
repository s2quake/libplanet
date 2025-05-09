using Libplanet.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Tests.Store;

public abstract class ProxyStore(IStore store) : IStore
{
    public virtual void Dispose() => store.Dispose();

    public virtual IEnumerable<Guid> ListChainIds() => store.ListChainIds();

    public virtual void DeleteChainId(Guid chainId) => store.DeleteChainId(chainId);

    public virtual Guid GetCanonicalChainId() => store.GetCanonicalChainId();

    public virtual void SetCanonicalChainId(Guid chainId) => store.SetCanonicalChainId(chainId);

    public virtual int CountIndex(Guid chainId) => store.CountIndex(chainId);

    public virtual IEnumerable<BlockHash> IterateIndexes(Guid chainId, int offset = 0, int? limit = null)
        => store.IterateIndexes(chainId, offset, limit);

    public virtual BlockHash GetBlockHash(Guid chainId, int height) => store.GetBlockHash(chainId, height);

    public virtual void AppendIndex(Guid chainId, int height, BlockHash hash) => store.AppendIndex(chainId, height, hash);

    public virtual void ForkBlockIndexes(
        Guid sourceChainId,
        Guid destinationChainId,
        BlockHash branchpoint) =>
        store.ForkBlockIndexes(sourceChainId, destinationChainId, branchpoint);

    public virtual Transaction GetTransaction(TxId txId) => store.GetTransaction(txId);

    public virtual void PutTransaction(Transaction tx) => store.PutTransaction(tx);

    public virtual IEnumerable<BlockHash> IterateBlockHashes() => store.IterateBlockHashes();

    public virtual Block GetBlock(BlockHash blockHash) => store.GetBlock(blockHash);

    public virtual int GetBlockHeight(BlockHash blockHash) => store.GetBlockHeight(blockHash);

    public virtual BlockDigest GetBlockDigest(BlockHash blockHash) => store.GetBlockDigest(blockHash);

    public virtual void PutBlock(Block block) => store.PutBlock(block);

    public virtual bool DeleteBlock(BlockHash blockHash) => store.DeleteBlock(blockHash);

    public virtual bool ContainsBlock(BlockHash blockHash) => store.ContainsBlock(blockHash);

    public virtual void PutTxExecution(TxExecution txExecution) => store.PutTxExecution(txExecution);

    public virtual TxExecution GetTxExecution(BlockHash blockHash, TxId txId) => store.GetTxExecution(blockHash, txId);

    public virtual void PutTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
        => store.PutTxIdBlockHashIndex(txId, blockHash);

    public virtual BlockHash GetFirstTxIdBlockHashIndex(TxId txId)
        => store.GetFirstTxIdBlockHashIndex(txId);

    public virtual IEnumerable<BlockHash> IterateTxIdBlockHashIndex(TxId txId)
        => store.IterateTxIdBlockHashIndex(txId);

    public virtual void DeleteTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
        => store.DeleteTxIdBlockHashIndex(txId, blockHash);

    public virtual IEnumerable<KeyValuePair<Address, long>> ListTxNonces(Guid chainId)
        => store.ListTxNonces(chainId);

    public virtual long GetTxNonce(Guid chainId, Address address)
        => store.GetTxNonce(chainId, address);

    public virtual void IncreaseTxNonce(Guid chainId, Address signer, long delta = 1)
        => store.IncreaseTxNonce(chainId, signer, delta);

    public virtual bool ContainsTransaction(TxId txId) => store.ContainsTransaction(txId);

    public virtual long CountBlocks() => store.CountBlocks();

    public virtual void ForkTxNonces(Guid sourceChainId, Guid destinationChainId)
        => store.ForkTxNonces(sourceChainId, destinationChainId);

    public void PruneOutdatedChains(bool noopWithoutCanon = false) => store.PruneOutdatedChains(noopWithoutCanon);

    public BlockCommit GetChainBlockCommit(Guid chainId) => store.GetChainBlockCommit(chainId);

    public void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit)
        => store.PutChainBlockCommit(chainId, blockCommit);

    public BlockCommit GetBlockCommit(BlockHash blockHash) => store.GetBlockCommit(blockHash);

    public void PutBlockCommit(BlockCommit blockCommit) => store.PutBlockCommit(blockCommit);

    public void DeleteBlockCommit(BlockHash blockHash) => store.DeleteBlockCommit(blockHash);

    public IEnumerable<BlockHash> GetBlockCommitHashes() => store.GetBlockCommitHashes();

    public IEnumerable<EvidenceId> IteratePendingEvidenceIds() => store.IteratePendingEvidenceIds();

    public EvidenceBase GetPendingEvidence(EvidenceId evidenceId) => store.GetPendingEvidence(evidenceId);

    public EvidenceBase GetCommittedEvidence(EvidenceId evidenceId) => store.GetCommittedEvidence(evidenceId);

    public void PutPendingEvidence(EvidenceBase evidence) => store.PutPendingEvidence(evidence);

    public void PutCommittedEvidence(EvidenceBase evidence) => store.PutCommittedEvidence(evidence);

    public void DeletePendingEvidence(EvidenceId evidenceId) => store.DeletePendingEvidence(evidenceId);

    public void DeleteCommittedEvidence(EvidenceId evidenceId) => store.DeleteCommittedEvidence(evidenceId);

    public bool ContainsPendingEvidence(EvidenceId evidenceId) => store.ContainsPendingEvidence(evidenceId);

    public bool ContainsCommittedEvidence(EvidenceId evidenceId) => store.ContainsCommittedEvidence(evidenceId);
}
