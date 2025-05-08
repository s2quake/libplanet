using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public class DefaultStore : StoreBase
{
    private const string IndexColPrefix = "index_";
    private const string TxNonceIdPrefix = "nonce_";
    private const string CommitColPrefix = "commit_";

    private readonly DefaultDatabase _database;
    private readonly TransactionCollection _transactions;
    private readonly BlockCollection _blocks;
    private readonly TxExecutionCollection _txExecutions;
    private readonly BlockHashByTxIdCollection _blockHashes;
    private readonly BlockCommitCollection _blockCommits;
    private readonly StateRootHashCollection _nextStateRootHashes;
    private readonly EvidenceCollection _pendingEvidence;
    private readonly EvidenceCollection _committedEvidence;
    private readonly HeightByChainId _heights;
    private readonly BlockCommitByChainId _blockCommitByChainId;
    private readonly StringCollection _metadata;

    private bool _disposed;

    public DefaultStore(DefaultStoreOptions options)
    {
        _database = new DefaultDatabase(options.Path);
        _transactions = new TransactionCollection(_database.GetOrAdd("tx"));
        _blocks = new BlockCollection(_database.GetOrAdd("block"));
        _txExecutions = new TxExecutionCollection(_database.GetOrAdd("txexec"));
        _blockHashes = new BlockHashByTxIdCollection(_database.GetOrAdd("txbindex"));
        _blockCommits = new BlockCommitCollection(_database.GetOrAdd("blockcommit"));
        _nextStateRootHashes = new StateRootHashCollection(_database.GetOrAdd("nextstateroothash"));
        _pendingEvidence = new EvidenceCollection(_database.GetOrAdd("evidencep"));
        _committedEvidence = new EvidenceCollection(_database.GetOrAdd("evidencec"));
        _heights = new HeightByChainId(_database.GetOrAdd("heights"));
        _blockCommitByChainId = new BlockCommitByChainId(_database.GetOrAdd("blockcommitb"));
        _metadata = new StringCollection(_database.GetOrAdd("metadata"));
        Options = options;
    }

    public DefaultStoreOptions Options { get; }

    public override IEnumerable<Guid> ListChainIds() => _heights.Keys;

    public override void DeleteChainId(Guid chainId)
    {
        _database.Remove(TxNonceKey(chainId));
        _database.Remove(IndexKey(chainId));
    }

    public override Guid GetCanonicalChainId() => Guid.Parse(_metadata["chainId"]);

    public override void SetCanonicalChainId(Guid chainId) => _metadata["chainId"] = chainId.ToString();

    public override long CountIndex(Guid chainId) => _heights[chainId];

    public override IEnumerable<BlockHash> IterateIndexes(Guid chainId, int offset, int? limit)
    {
        var collection = IndexCollection(chainId);
        for (var i = offset; i < (limit ?? int.MaxValue); i++)
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

    public override BlockHash GetBlockHash(Guid chainId, long height)
    {
        var collection = IndexCollection(chainId);
        return collection[height];
    }

    public override void AppendIndex(Guid chainId, long height, BlockHash hash)
    {
        var collection = IndexCollection(chainId);
        collection.Add(height, hash);
        _heights[chainId] = height;
    }

    public override void ForkBlockIndexes(
        Guid sourceChainId,
        Guid destinationChainId,
        BlockHash branchpoint)
    {
        // LiteCollection<HashDoc> srcColl = IndexKey(sourceChainId);
        // LiteCollection<HashDoc> destColl = IndexKey(destinationChainId);

        // BlockHash? genesisHash = IterateIndexes(sourceChainId, 0, 1)
        //     .Cast<BlockHash?>()
        //     .FirstOrDefault();

        // if (genesisHash is null || branchpoint.Equals(genesisHash))
        // {
        //     return;
        // }

        // destColl.Delete(Query.All());
        // destColl.InsertBulk(srcColl.FindAll().TakeWhile(i => !i.Hash.Equals(branchpoint)));

        // AppendIndex(destinationChainId, branchpoint);
    }

    public override Transaction GetTransaction(TxId txId)
    {
        return _transactions[txId];
    }

    public override void PutTransaction(Transaction tx)
    {
        _transactions.Add(tx.Id, tx);
    }

    public override bool ContainsTransaction(TxId txId)
    {
        return _transactions.ContainsKey(txId);
    }

    public override IEnumerable<BlockHash> IterateBlockHashes()
    {
        return _blocks.Keys;
    }

    public override BlockDigest GetBlockDigest(BlockHash blockHash)
    {
        return _blocks.GetBlockDigest(blockHash);
    }

    public override void PutBlock(Block block)
    {
        _blocks.Add(block.BlockHash, block);
    }

    public override bool DeleteBlock(BlockHash blockHash)
    {
        return _blocks.Remove(blockHash);
    }

    public override bool ContainsBlock(BlockHash blockHash)
    {
        return _blocks.ContainsKey(blockHash);
    }

    public override void PutTxExecution(TxExecution txExecution)
    {
        _txExecutions.Add(txExecution);
    }

    public override TxExecution GetTxExecution(BlockHash blockHash, TxId txId)
    {
        return _txExecutions[(blockHash, txId)];
    }

    public override void PutTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
    {
        if (_blockHashes.TryGetValue(txId, out var blockHashes))
        {
            blockHashes = ImmutableArray<BlockHash>.Empty;
        }

        _blockHashes.Add(txId, blockHashes.Add(blockHash));
    }

    public override IEnumerable<BlockHash> IterateTxIdBlockHashIndex(TxId txId)
    {
        if (_blockHashes.TryGetValue(txId, out var blockHashes))
        {
            return blockHashes;
        }

        return [];
    }

    public override void DeleteTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
    {
        if (_blockHashes.TryGetValue(txId, out var blockHashes))
        {
            blockHashes = blockHashes.Remove(blockHash);
            if (blockHashes.IsEmpty)
            {
                _blockHashes.Remove(txId);
            }
            else
            {
                _blockHashes[txId] = blockHashes;
            }
        }
    }

    public override IEnumerable<KeyValuePair<Address, long>> ListTxNonces(Guid chainId)
    {
        var collection = new NonceCollection(_database.GetOrAdd(TxNonceKey(chainId)));
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

    public override long GetTxNonce(Guid chainId, Address address)
    {
        var collection = new NonceCollection(_database.GetOrAdd(TxNonceKey(chainId)));
        if (collection.TryGetValue(address, out var nonce))
        {
            return nonce;
        }

        return 0L;
    }

    public override void IncreaseTxNonce(Guid chainId, Address signer, long delta = 1)
    {
        var collection = new NonceCollection(_database.GetOrAdd(TxNonceKey(chainId)));
        if (!collection.TryGetValue(signer, out var nonce))
        {
            nonce = 0L;
        }

        collection[signer] = nonce + delta;
    }

    public override void ForkTxNonces(Guid sourceChainId, Guid destinationChainId)
    {
        // LiteCollection<BsonDocument> srcColl = TxNonceCollection(sourceChainId);
        // LiteCollection<BsonDocument> destColl = TxNonceCollection(destinationChainId);
        // destColl.InsertBulk(srcColl.FindAll());
    }

    public override void PruneOutdatedChains(bool noopWithoutCanon = false)
    {
        if (!(GetCanonicalChainId() is { } ccid))
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

    public override BlockCommit GetChainBlockCommit(Guid chainId)
    {
        return _blockCommitByChainId[chainId];
    }

    public override void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit)
    {
        _blockCommitByChainId[chainId] = blockCommit;
    }

    public override BlockCommit GetBlockCommit(BlockHash blockHash)
    {
        return _blockCommits[blockHash];
    }

    public override void PutBlockCommit(BlockCommit blockCommit)
    {
        _blockCommits.Add(blockCommit.BlockHash, blockCommit);
    }

    public override void DeleteBlockCommit(BlockHash blockHash)
    {
        _blockCommits.Remove(blockHash);
    }

    public override IEnumerable<BlockHash> GetBlockCommitHashes()
    {
        return _blockCommits.Keys;
    }

    public override HashDigest<SHA256> GetNextStateRootHash(BlockHash blockHash)
    {
        return _nextStateRootHashes[blockHash];
    }

    public override void PutNextStateRootHash(BlockHash blockHash, HashDigest<SHA256> nextStateRootHash)
    {
        _nextStateRootHashes.Add(blockHash, nextStateRootHash);
    }

    public override void DeleteNextStateRootHash(BlockHash blockHash)
    {
        _nextStateRootHashes.Remove(blockHash);
    }

    public override IEnumerable<EvidenceId> IteratePendingEvidenceIds()
    {
        return _pendingEvidence.Keys;
    }

    public override EvidenceBase GetPendingEvidence(EvidenceId evidenceId)
    {
        return _pendingEvidence[evidenceId];
    }

    public override void PutPendingEvidence(EvidenceBase evidence)
    {
        _pendingEvidence.Add(evidence.Id, evidence);
    }

    public override void DeletePendingEvidence(EvidenceId evidenceId)
    {
        _pendingEvidence.Remove(evidenceId);
    }

    public override bool ContainsPendingEvidence(EvidenceId evidenceId)
    {
        return _pendingEvidence.ContainsKey(evidenceId);
    }

    public override EvidenceBase GetCommittedEvidence(EvidenceId evidenceId)
    {
        return _committedEvidence[evidenceId];
    }

    public override void PutCommittedEvidence(EvidenceBase evidence)
    {
        _committedEvidence.Add(evidence.Id, evidence);
    }

    public override void DeleteCommittedEvidence(EvidenceId evidenceId)
    {
        _committedEvidence.Remove(evidenceId);
    }

    public override bool ContainsCommittedEvidence(EvidenceId evidenceId)
    {
        return _committedEvidence.ContainsKey(evidenceId);
    }

    public override long CountBlocks()
    {
        // FIXME: This implementation is too inefficient.  Fortunately, this method seems
        // unused (except for unit tests).  If this is never used why should we maintain
        // this?  This is basically only for making BlockSet<T> class to implement
        // IDictionary<HashDigest<SHA256>, Block>.Count property, which is never used either.
        // We'd better to refactor all such things so that unnecessary APIs are gone away.
        return IterateBlockHashes().LongCount();
    }

    public override void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    internal static Guid ParseChainId(string chainIdString) =>
        new Guid(ByteUtility.ParseHex(chainIdString));

    internal static string FormatChainId(Guid chainId) =>
        ByteUtility.Hex(chainId.ToByteArray());

    private static string IndexKey(in Guid chainId)
        => $"{IndexColPrefix}{FormatChainId(chainId)}";

    private static string TxNonceKey(Guid chainId)
        => $"{TxNonceIdPrefix}{FormatChainId(chainId)}";

    private BlockHashCollection IndexCollection(in Guid chainId) =>
        new(_database.GetOrAdd(IndexKey(chainId)));
}
