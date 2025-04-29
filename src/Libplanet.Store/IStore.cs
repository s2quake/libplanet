using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public interface IStore : IDisposable
{
    IEnumerable<Guid> ListChainIds();

    void DeleteChainId(Guid chainId);

    Guid? GetCanonicalChainId();

    void SetCanonicalChainId(Guid chainId);

    long CountIndex(Guid chainId);

    IEnumerable<BlockHash> IterateIndexes(Guid chainId, int offset = 0, int? limit = null);

    BlockHash? IndexBlockHash(Guid chainId, long index);

    long AppendIndex(Guid chainId, BlockHash hash);

    void ForkBlockIndexes(Guid sourceChainId, Guid destinationChainId, BlockHash branchpoint);

    Transaction? GetTransaction(TxId txid);

    void PutTransaction(Transaction tx);

    IEnumerable<BlockHash> IterateBlockHashes();

    Block? GetBlock(BlockHash blockHash);

    long? GetBlockIndex(BlockHash blockHash);

    BlockDigest GetBlockDigest(BlockHash blockHash);

    void PutBlock(Block block);

    bool DeleteBlock(BlockHash blockHash);

    bool ContainsBlock(BlockHash blockHash);

    void PutTxExecution(TxExecution txExecution);

    TxExecution? GetTxExecution(BlockHash blockHash, TxId txid);

    void PutTxIdBlockHashIndex(TxId txId, BlockHash blockHash);

    BlockHash? GetFirstTxIdBlockHashIndex(TxId txId);

    IEnumerable<BlockHash> IterateTxIdBlockHashIndex(TxId txId);

    void DeleteTxIdBlockHashIndex(TxId txId, BlockHash blockHash);

    IEnumerable<KeyValuePair<Address, long>> ListTxNonces(Guid chainId);

    long GetTxNonce(Guid chainId, Address address);

    void IncreaseTxNonce(Guid chainId, Address signer, long delta = 1);

    bool ContainsTransaction(TxId txId);

    long CountBlocks();

    void ForkTxNonces(Guid sourceChainId, Guid destinationChainId);

    void PruneOutdatedChains(bool noopWithoutCanon = false);

    BlockCommit GetChainBlockCommit(Guid chainId);

    void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit);

    BlockCommit GetBlockCommit(BlockHash blockHash);

    void PutBlockCommit(BlockCommit blockCommit);

    void DeleteBlockCommit(BlockHash blockHash);

    IEnumerable<BlockHash> GetBlockCommitHashes();

    IEnumerable<EvidenceId> IteratePendingEvidenceIds();

    EvidenceBase? GetPendingEvidence(EvidenceId evidenceId);

    EvidenceBase? GetCommittedEvidence(EvidenceId evidenceId);

    void PutPendingEvidence(EvidenceBase evidence);

    void PutCommittedEvidence(EvidenceBase evidence);

    void DeletePendingEvidence(EvidenceId evidenceId);

    void DeleteCommittedEvidence(EvidenceId evidenceId);

    bool ContainsPendingEvidence(EvidenceId evidenceId);

    bool ContainsCommittedEvidence(EvidenceId evidenceId);
}
