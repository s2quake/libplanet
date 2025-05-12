using System.Collections.Concurrent;
using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class Store
{
    private const string IndexColPrefix = "index_";
    private const string TxNonceIdPrefix = "nonce_";
    private static readonly object _lock = new();

    private readonly IDatabase _database;
    private readonly TransactionCollection _transactions;
    private readonly TxExecutionCollection _txExecutions;
    private readonly BlockHashesByTxId _blockHashesByTxId;
    private readonly BlockDigestCollection _blockDigests;
    private readonly BlockCommitCollection _blockCommits;
    private readonly BlockHashCollection _blockHashes;
    private readonly StateRootHashCollection _stateRootHashes;
    private readonly PendingEvidenceCollection _pendingEvidence;
    private readonly CommittedEvidenceCollection _committedEvidence;
    private readonly HeightByChainId _heightByChainId;
    private readonly BlockCommitByChainId _blockCommitByChainId;
    private readonly StringCollection _metadata;
    private readonly BlockCollection _blocks;
    private readonly ConcurrentDictionary<Guid, NonceCollection> _noncesByChainId = new();
    private NonceCollection? _nonces;

    private bool _disposed;

    public Store(IDatabase database)
    {
        _database = database;
        _transactions = new TransactionCollection(_database.GetOrAdd("tx"));
        _blockDigests = new BlockDigestCollection(_database.GetOrAdd("block"));
        _txExecutions = new TxExecutionCollection(_database.GetOrAdd("txexec"));
        _blockHashesByTxId = new BlockHashesByTxId(_database.GetOrAdd("txbindex"));
        _blockCommits = new BlockCommitCollection(_database.GetOrAdd("blockcommit"));
        _blockHashes = new BlockHashCollection(_database.GetOrAdd("blockhash"));
        _stateRootHashes = new StateRootHashCollection(_database.GetOrAdd("nextstateroothash"));
        _pendingEvidence = new PendingEvidenceCollection(_database.GetOrAdd("evidencep"));
        _committedEvidence = new CommittedEvidenceCollection(_database.GetOrAdd("evidencec"));
        _heightByChainId = new HeightByChainId(_database.GetOrAdd("heights"));
        _blockCommitByChainId = new BlockCommitByChainId(_database.GetOrAdd("blockcommitb"));
        _metadata = new StringCollection(_database.GetOrAdd("metadata"));
        _blocks = new BlockCollection(this);
    }

    public PendingEvidenceCollection PendingEvidences => _pendingEvidence;

    public CommittedEvidenceCollection CommittedEvidences => _committedEvidence;

    public TransactionCollection Transactions => _transactions;

    public BlockCommitCollection BlockCommits => _blockCommits;

    public BlockDigestCollection BlockDigests => _blockDigests;

    public BlockHashCollection BlockHashes => _blockHashes;

    public BlockCollection Blocks => _blocks;

    public TxExecutionCollection TxExecutions => _txExecutions;

    public NonceCollection Nonces => _nonces ?? throw new InvalidOperationException("Chain ID is not set.");

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
            _nonces = GetNonceCollection(value);
        }
    }

    public NonceCollection GetNonceCollection(Guid chainId)
    {
        lock (_lock)
        {
            if (!_noncesByChainId.TryGetValue(chainId, out var nonces))
            {
                nonces = new NonceCollection(_database.GetOrAdd(TxNonceKey(chainId)));
                _noncesByChainId.TryAdd(chainId, nonces);
            }

            return nonces;
        }
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

    public int CountIndex(Guid chainId)
    {
        if (!_heightByChainId.ContainsKey(chainId))
        {
            throw new KeyNotFoundException("Chain ID not found.");
        }

        return IndexCollection(chainId).Count;
    }

    public IEnumerable<BlockHash> IterateIndexes(Guid chainId, int offset = 0, int? limit = null)
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

    // public void PutTxExecution(TxExecution txExecution) => _txExecutions.Add(txExecution);

    // public TxExecution GetTxExecution(BlockHash blockHash, TxId txId) => _txExecutions[(blockHash, txId)];

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

    // public IEnumerable<KeyValuePair<Address, long>> ListTxNonces(Guid chainId)
    // {
    //     var collection = new NonceCollection(_database.GetOrAdd(TxNonceKey(chainId)));
    //     foreach (var item in collection)
    //     {
    //         yield return item;
    //     }
    // }

    // public long GetTxNonce(Guid chainId, Address address)
    // {
    //     var collection = new NonceCollection(_database.GetOrAdd(TxNonceKey(chainId)));
    //     if (collection.TryGetValue(address, out var nonce))
    //     {
    //         return nonce;
    //     }

    //     return 0L;
    // }

    // public void IncreaseTxNonce(Guid chainId, Address signer, long delta = 1)
    // {
    //     var collection = new NonceCollection(_database.GetOrAdd(TxNonceKey(chainId)));
    //     if (!collection.TryGetValue(signer, out var nonce))
    //     {
    //         nonce = 0L;
    //     }

    //     collection[signer] = nonce + delta;
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

        Guid[] chainIds = ListChainIds().ToArray();
        foreach (Guid id in chainIds.Where(id => !id.Equals(ccid)))
        {
            DeleteChainId(id);
        }
    }

    public BlockCommit GetChainBlockCommit(Guid chainId) => _blockCommitByChainId[chainId];

    public void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit)
        => _blockCommitByChainId[chainId] = blockCommit;

    public IEnumerable<BlockHash> GetBlockCommitHashes() => _blockCommits.Keys;

    public HashDigest<SHA256> GetNextStateRootHash(BlockHash blockHash) => _stateRootHashes[blockHash];

    public void PutNextStateRootHash(BlockHash blockHash, HashDigest<SHA256> nextStateRootHash)
        => _stateRootHashes.Add(blockHash, nextStateRootHash);

    public void DeleteNextStateRootHash(BlockHash blockHash) => _stateRootHashes.Remove(blockHash);

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

    private NonceCollection GetNonceCollection(in Guid chainId) => new(_database.GetOrAdd(TxNonceKey(chainId)));
}
