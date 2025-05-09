using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class Store : IStore
{
    private const string IndexColPrefix = "index_";
    private const string TxNonceIdPrefix = "nonce_";

    private readonly IDatabase _database;
    private readonly TransactionCollection _transactions;
    private readonly BlockCollection _blocks;
    private readonly TxExecutionCollection _txExecutions;
    private readonly BlockHashesByTxId _blockHashesByTxId;
    private readonly BlockCommitByBlockHash _blockCommitByBlockHash;
    private readonly StateRootHashByBlockHash _stateRootHashByBlockHash;
    private readonly EvidenceCollection _pendingEvidence;
    private readonly EvidenceCollection _committedEvidence;
    private readonly HeightByChainId _heightByChainId;
    private readonly BlockCommitByChainId _blockCommitByChainId;
    private readonly StringCollection _metadata;

    private bool _disposed;

    public Store(IDatabase database)
    {
        _database = database;
        _transactions = new TransactionCollection(_database.GetOrAdd("tx"));
        _blocks = new BlockCollection(_database.GetOrAdd("block"));
        _txExecutions = new TxExecutionCollection(_database.GetOrAdd("txexec"));
        _blockHashesByTxId = new BlockHashesByTxId(_database.GetOrAdd("txbindex"));
        _blockCommitByBlockHash = new BlockCommitByBlockHash(_database.GetOrAdd("blockcommit"));
        _stateRootHashByBlockHash = new StateRootHashByBlockHash(_database.GetOrAdd("nextstateroothash"));
        _pendingEvidence = new EvidenceCollection(_database.GetOrAdd("evidencep"));
        _committedEvidence = new EvidenceCollection(_database.GetOrAdd("evidencec"));
        _heightByChainId = new HeightByChainId(_database.GetOrAdd("heights"));
        _blockCommitByChainId = new BlockCommitByChainId(_database.GetOrAdd("blockcommitb"));
        _metadata = new StringCollection(_database.GetOrAdd("metadata"));
    }

    public Block GetBlock(BlockHash blockHash)
        => GetBlockDigest(blockHash).ToBlock(GetTransaction, GetCommittedEvidence);

    public int GetBlockHeight(BlockHash blockHash)
    {
        return GetBlockDigest(blockHash).Height;
    }

    public BlockHash GetFirstTxIdBlockHashIndex(TxId txId)
    {
        var item = IterateTxIdBlockHashIndex(txId).FirstOrDefault();
        if (item == default)
        {
            throw new KeyNotFoundException(
                $"The transaction ID {txId} does not exist in the index.");
        }

        return item;
    }

    public IEnumerable<Guid> ListChainIds() => _heightByChainId.Keys;

    public void DeleteChainId(Guid chainId)
    {
        _blockCommitByChainId.Remove(chainId);
        _heightByChainId.Remove(chainId);
        _database.Remove(TxNonceKey(chainId));
        _database.Remove(IndexKey(chainId));
    }

    public Guid GetCanonicalChainId()
    {
        if (!_metadata.TryGetValue("chainId", out var chainId))
        {
            return Guid.Empty;
        }

        return Guid.Parse(chainId);
    }

    public void SetCanonicalChainId(Guid chainId) => _metadata["chainId"] = chainId.ToString();

    public int CountIndex(Guid chainId)
    {
        if (!_heightByChainId.ContainsKey(chainId))
        {
            throw new KeyNotFoundException("Chain ID not found.");
        }

        return IndexCollection(chainId).Count;
    }

    public IEnumerable<BlockHash> IterateIndexes(Guid chainId, int offset, int? limit)
    {
        var collection = IndexCollection(chainId);
        var end = checked(limit is { } l ? offset + l : int.MaxValue);
        for (var i = offset; i < end; i++)
        {
            if (collection.TryGetValue(i, out var blockHash))
            {
                yield return blockHash;
            }
            else
            {
                break;
            }
        }
    }

    public BlockHash GetBlockHash(Guid chainId, int height)
    {
        var collection = IndexCollection(chainId);
        if (height < 0)
        {
            height += collection.Count;
        }

        return collection[height];
    }

    public int AppendIndex(Guid chainId, BlockHash hash)
    {
        var collection = IndexCollection(chainId);
        var height = collection.Count;
        collection.Add(height, hash);
        _heightByChainId[chainId] = height;
        return height;
    }

    public void ForkBlockIndexes(Guid sourceChainId, Guid destinationChainId, BlockHash branchpoint)
    {
        var srcColl = IndexCollection(sourceChainId);
        var destColl = IndexCollection(destinationChainId);

        BlockHash genesisHash = IterateIndexes(sourceChainId, 0, 1).FirstOrDefault();

        if (genesisHash == default || branchpoint.Equals(genesisHash))
        {
            return;
        }

        destColl.Clear();
        for (var i = 0; i < srcColl.Count; i++)
        {
            var item = srcColl[i];
            if (item.Equals(branchpoint))
            {
                break;
            }

            destColl.Add(i, item);
        }

        AppendIndex(destinationChainId, branchpoint);
    }

    public Transaction GetTransaction(TxId txId)
    {
        return _transactions[txId];
    }

    public void PutTransaction(Transaction tx)
    {
        _transactions[tx.Id] = tx;
    }

    public bool ContainsTransaction(TxId txId)
    {
        return _transactions.ContainsKey(txId);
    }

    public IEnumerable<BlockHash> IterateBlockHashes()
    {
        return _blocks.Keys;
    }

    public BlockDigest GetBlockDigest(BlockHash blockHash)
    {
        return _blocks.GetBlockDigest(blockHash);
    }

    public void PutBlock(Block block)
    {
        _blocks.Add(block.BlockHash, block);

        foreach (Transaction tx in block.Transactions)
        {
            PutTransaction(tx);
        }
    }

    public bool DeleteBlock(BlockHash blockHash)
    {
        return _blocks.Remove(blockHash);
    }

    public bool ContainsBlock(BlockHash blockHash)
    {
        return _blocks.ContainsKey(blockHash);
    }

    public void PutTxExecution(TxExecution txExecution)
    {
        _txExecutions.Add(txExecution);
    }

    public TxExecution GetTxExecution(BlockHash blockHash, TxId txId)
    {
        return _txExecutions[(blockHash, txId)];
    }

    public void PutTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
    {
        if (!_blockHashesByTxId.TryGetValue(txId, out var blockHashes))
        {
            blockHashes = [];
        }

        _blockHashesByTxId[txId] = blockHashes.Add(blockHash);
    }

    public IEnumerable<BlockHash> IterateTxIdBlockHashIndex(TxId txId)
    {
        if (_blockHashesByTxId.TryGetValue(txId, out var blockHashes))
        {
            return blockHashes;
        }

        return [];
    }

    public void DeleteTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
    {
        if (_blockHashesByTxId.TryGetValue(txId, out var blockHashes))
        {
            blockHashes = blockHashes.Remove(blockHash);
            if (blockHashes.IsEmpty)
            {
                _blockHashesByTxId.Remove(txId);
            }
            else
            {
                _blockHashesByTxId[txId] = blockHashes;
            }
        }
    }

    public IEnumerable<KeyValuePair<Address, long>> ListTxNonces(Guid chainId)
    {
        var collection = new NonceByAddress(_database.GetOrAdd(TxNonceKey(chainId)));
        foreach (var item in collection)
        {
            yield return item;
        }

        // LiteCollection<BsonDocument> collection = TxNonceCollection(chainId);
        // foreach (BsonDocument doc in collection.FindAll())
        // {
        //     if (doc.TryGetValue("_id", out BsonValue id) && id.IsBinary)
        //     {
        //         var address = new Address([.. id.AsBinary]);
        //         if (doc.TryGetValue("v", out BsonValue v) && v.IsInt64 && v.AsInt64 > 0)
        //         {
        //             yield return new KeyValuePair<Address, long>(address, v.AsInt64);
        //         }
        //     }
        // }
    }

    public long GetTxNonce(Guid chainId, Address address)
    {
        var collection = new NonceByAddress(_database.GetOrAdd(TxNonceKey(chainId)));
        if (collection.TryGetValue(address, out var nonce))
        {
            return nonce;
        }

        return 0L;
    }

    public void IncreaseTxNonce(Guid chainId, Address signer, long delta = 1)
    {
        var collection = new NonceByAddress(_database.GetOrAdd(TxNonceKey(chainId)));
        if (!collection.TryGetValue(signer, out var nonce))
        {
            nonce = 0L;
        }

        collection[signer] = nonce + delta;
    }

    public void ForkTxNonces(Guid sourceChainId, Guid destinationChainId)
    {
        var srcColl = TxNonceCollection(sourceChainId);
        var destColl = TxNonceCollection(destinationChainId);

        if (destColl.Count > 0)
        {
            throw new InvalidOperationException("Destination chain ID already has nonces.");
        }

        foreach (var item in srcColl)
        {
            destColl.Add(item.Key, item.Value);
        }
    }

    public void PruneOutdatedChains(bool noopWithoutCanon = false)
    {
        var ccid = GetCanonicalChainId();
        if (ccid == Guid.Empty)
        {
            if (noopWithoutCanon)
            {
                return;
            }

            throw new InvalidOperationException("Canonical chain ID is not assigned.");
        }

        Guid[] chainIds = ListChainIds().ToArray();
        foreach (Guid id in chainIds.Where(id => !id.Equals(ccid)))
        {
            DeleteChainId(id);
        }
    }

    public BlockCommit GetChainBlockCommit(Guid chainId) => _blockCommitByChainId[chainId];

    public void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit)
        => _blockCommitByChainId[chainId] = blockCommit;

    public BlockCommit GetBlockCommit(BlockHash blockHash) => _blockCommitByBlockHash[blockHash];

    public void PutBlockCommit(BlockCommit blockCommit)
        => _blockCommitByBlockHash.Add(blockCommit.BlockHash, blockCommit);

    public void DeleteBlockCommit(BlockHash blockHash) => _blockCommitByBlockHash.Remove(blockHash);

    public IEnumerable<BlockHash> GetBlockCommitHashes() => _blockCommitByBlockHash.Keys;

    public HashDigest<SHA256> GetNextStateRootHash(BlockHash blockHash) => _stateRootHashByBlockHash[blockHash];

    public void PutNextStateRootHash(BlockHash blockHash, HashDigest<SHA256> nextStateRootHash)
        => _stateRootHashByBlockHash.Add(blockHash, nextStateRootHash);

    public void DeleteNextStateRootHash(BlockHash blockHash) => _stateRootHashByBlockHash.Remove(blockHash);

    public IEnumerable<EvidenceId> IteratePendingEvidenceIds() => _pendingEvidence.Keys;

    public EvidenceBase GetPendingEvidence(EvidenceId evidenceId) => _pendingEvidence[evidenceId];

    public void PutPendingEvidence(EvidenceBase evidence) => _pendingEvidence.Add(evidence.Id, evidence);

    public void DeletePendingEvidence(EvidenceId evidenceId) => _pendingEvidence.Remove(evidenceId);

    public bool ContainsPendingEvidence(EvidenceId evidenceId) => _pendingEvidence.ContainsKey(evidenceId);

    public EvidenceBase GetCommittedEvidence(EvidenceId evidenceId) => _committedEvidence[evidenceId];

    public void PutCommittedEvidence(EvidenceBase evidence) => _committedEvidence.Add(evidence.Id, evidence);

    public void DeleteCommittedEvidence(EvidenceId evidenceId) => _committedEvidence.Remove(evidenceId);

    public bool ContainsCommittedEvidence(EvidenceId evidenceId) => _committedEvidence.ContainsKey(evidenceId);

    public long CountBlocks() => IterateBlockHashes().LongCount();

    public void Dispose()
    {
        if (!_disposed)
        {
            _database.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    internal static Guid ParseChainId(string chainIdString) => new(ByteUtility.ParseHex(chainIdString));

    internal static string FormatChainId(Guid chainId) => ByteUtility.Hex(chainId.ToByteArray());

    private static string IndexKey(in Guid chainId) => $"{IndexColPrefix}{FormatChainId(chainId)}";

    private static string TxNonceKey(Guid chainId) => $"{TxNonceIdPrefix}{FormatChainId(chainId)}";

    private BlockHashByHeight IndexCollection(in Guid chainId) => new(_database.GetOrAdd(IndexKey(chainId)));

    private NonceByAddress TxNonceCollection(in Guid chainId) => new(_database.GetOrAdd(TxNonceKey(chainId)));
}
