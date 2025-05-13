using System.Collections.Concurrent;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class Store
{
    private static readonly object _lock = new();

    private readonly IDatabase _database;
    private readonly TransactionStore _transactions;
    private readonly TxExecutionStore _txExecutions;
    private readonly BlockDigestStore _blockDigests;
    private readonly BlockCommitStore _blockCommits;
    private readonly PendingEvidenceStore _pendingEvidences;
    private readonly CommittedEvidenceStore _committedEvidences;
    private readonly ChainDigestCollection _chainDigests;
    private readonly MetadataStore _metadata;
    private readonly BlockHashesByTxId _blockHashesByTxId;

    private readonly ConcurrentDictionary<Guid, NonceCollection> _noncesByChainId = new();
    private readonly ConcurrentDictionary<Guid, BlockHashStore> _blockHashByChainId = new();

    private bool _disposed;

    public Store(IDatabase database)
    {
        _database = database;
        _transactions = new TransactionStore(_database.GetOrAdd("tx"));
        _blockDigests = new BlockDigestStore(_database.GetOrAdd("block"));
        _txExecutions = new TxExecutionStore(_database.GetOrAdd("txexec"));
        _blockCommits = new BlockCommitStore(_database.GetOrAdd("blockcommit"));
        _pendingEvidences = new PendingEvidenceStore(_database.GetOrAdd("evidencep"));
        _committedEvidences = new CommittedEvidenceStore(_database.GetOrAdd("evidencec"));
        _chainDigests = new ChainDigestCollection(_database.GetOrAdd("chaindigest"));
        _metadata = new MetadataStore(_database.GetOrAdd("metadata"));
        _blockHashesByTxId = new BlockHashesByTxId(_database.GetOrAdd("txidblockhash"));
    }

    public PendingEvidenceStore PendingEvidences => _pendingEvidences;

    public CommittedEvidenceStore CommittedEvidences => _committedEvidences;

    public TransactionStore Transactions => _transactions;

    public BlockCommitStore BlockCommits => _blockCommits;

    public BlockDigestStore BlockDigests => _blockDigests;

    public TxExecutionStore TxExecutions => _txExecutions;

    public ChainDigestCollection ChainDigests => _chainDigests;

    public BlockHashesByTxId BlockHashesByTxId => _blockHashesByTxId;

    public Guid ChainId
    {
        get => _metadata.TryGetValue("chainId", out var chainId) ? Guid.Parse(chainId) : Guid.Empty;
        set
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("Chain ID cannot be empty.", nameof(value));
            }

            _metadata["chainId"] = value.ToString();

            // _blocks = GetBlockCollection(value);
            // _blockHashes = GetBlockHashes(value);
            // _nonces = GetNonceCollection(value);
        }
    }

    // public BlockStore Blocks => _blocks ?? throw new InvalidOperationException("Chain ID is not assigned.");

    // public BlockHashStore BlockHashes
    //     => _blockHashes ?? throw new InvalidOperationException("Chain ID is not assigned.");

    // public NonceCollection Nonces => _nonces ?? throw new InvalidOperationException("Chain ID is not assigned.");

    public NonceCollection GetNonceCollection(Guid chainId)
    {
        lock (_lock)
        {
            if (!_chainDigests.ContainsKey(chainId))
            {
                _chainDigests[chainId] = new ChainDigest
                {
                    Id = chainId,
                };
            }

            if (!_noncesByChainId.TryGetValue(chainId, out var nonces))
            {
                nonces = new NonceCollection(_database.GetOrAdd($"nonce_{chainId}"));
                _noncesByChainId.TryAdd(chainId, nonces);
            }

            return nonces;
        }
    }

    public BlockHashStore GetBlockHashes(Guid chainId)
    {
        lock (_lock)
        {
            if (!_chainDigests.ContainsKey(chainId))
            {
                _chainDigests[chainId] = new ChainDigest
                {
                    Id = chainId,
                };
            }

            if (!_blockHashByChainId.TryGetValue(chainId, out var blockHashes))
            {
                blockHashes = new BlockHashStore(_database.GetOrAdd($"blockhash_{chainId}"));
                _blockHashByChainId.TryAdd(chainId, blockHashes);
            }

            return blockHashes;
        }
    }

    // public BlockStore GetBlockCollection(Guid chainId)
    // {
    //     lock (_lock)
    //     {
    //         if (!_chainDigests.ContainsKey(chainId))
    //         {
    //             _chainDigests[chainId] = new ChainDigest
    //             {
    //                 Id = chainId,
    //             };
    //         }

    //         if (!_blocksByChainId.TryGetValue(chainId, out var blocks))
    //         {
    //             blocks = new BlockStore(this, chainId);
    //             _blocksByChainId.TryAdd(chainId, blocks);
    //         }

    //         return blocks;
    //     }
    // }

    // public BlockHash BlockHashByTxId[TxId txId]
    // {
    //     var item = IterateTxIdBlockHashIndex(txId).FirstOrDefault();
    //     if (item == default)
    //     {
    //         throw new KeyNotFoundException(
    //             $"The transaction ID {txId} does not exist in the index.");
    //     }

    //     return item;
    // }

    public void ForkBlockIndexes(Guid sourceChainId, Guid destinationChainId, BlockHash branchpoint)
    {
        // var srcColl = IndexCollection(sourceChainId);
        // var destColl = IndexCollection(destinationChainId);

        // BlockHash genesisHash = IterateIndexes(sourceChainId, 0, 1).FirstOrDefault();

        // if (genesisHash == default || branchpoint.Equals(genesisHash))
        // {
        //     return;
        // }

        // destColl.Clear();
        // for (var i = 0; i < srcColl.Count; i++)
        // {
        //     var item = srcColl[i];
        //     if (item.Equals(branchpoint))
        //     {
        //         break;
        //     }

        //     destColl.Add(i, item);
        // }

        // AppendIndex(destinationChainId, branchpoint);
    }

    // public void PutTxExecution(TxExecution txExecution) => _txExecutions.Add(txExecution);

    // public TxExecution GetTxExecution(BlockHash blockHash, TxId txId) => _txExecutions[(blockHash, txId)];

    // public void BlockHashByTxId.Add(TxId txId, BlockHash blockHash)
    // {
    //     if (!_blockHashesByTxId.TryGetValue(txId, out var blockHashes))
    //     {
    //         blockHashes = [];
    //     }

    //     _blockHashesByTxId[txId] = blockHashes.Add(blockHash);
    // }

    // public IEnumerable<BlockHash> IterateTxIdBlockHashIndex(TxId txId)
    // {
    //     if (_blockHashesByTxId.TryGetValue(txId, out var blockHashes))
    //     {
    //         return blockHashes;
    //     }

    //     return [];
    // }

    // public void BlockHashByTxId.Remove(TxId txId, BlockHash blockHash)
    // {
    //     if (_blockHashesByTxId.TryGetValue(txId, out var blockHashes))
    //     {
    //         blockHashes = blockHashes.Remove(blockHash);
    //         if (blockHashes.IsEmpty)
    //         {
    //             _blockHashesByTxId.Remove(txId);
    //         }
    //         else
    //         {
    //             _blockHashesByTxId[txId] = blockHashes;
    //         }
    //     }
    // }

    public void ForkTxNonces(Guid sourceChainId, Guid destinationChainId)
    {
        var srcColl = GetNonceCollection(sourceChainId);
        var destColl = GetNonceCollection(destinationChainId);

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
        var ccid = ChainId;
        if (ccid == Guid.Empty)
        {
            if (noopWithoutCanon)
            {
                return;
            }

            throw new InvalidOperationException("Canonical chain ID is not assigned.");
        }

        Guid[] chainIds = ChainDigests.Keys.ToArray();
        foreach (Guid id in chainIds.Where(id => !id.Equals(ccid)))
        {
            ChainDigests.Remove(id);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _database.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
