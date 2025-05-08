using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;
using LiteDB;
using FileMode = LiteDB.FileMode;

namespace Libplanet.Store;

public class DefaultStore : StoreBase
{
    private const string IndexColPrefix = "index_";
    private const string TxNonceIdPrefix = "nonce_";
    private const string CommitColPrefix = "commit_";
    private const string StatesKvPathDefault = "states";

    private readonly DefaultDatabase _database;
    private readonly TransactionCollection _transactions;
    private readonly BlockCollection _blocks;
    private readonly TxExecutionCollection _txExecutions;
    private readonly BlockHashCollection _blockHashes;
    private readonly BlockCommitCollection _blockCommits;
    private readonly StateRootHashCollection _nextStateRootHashes;
    private readonly EvidenceCollection _pendingEvidence;
    private readonly EvidenceCollection _committedEvidence;

    private readonly LiteDatabase _db;

    private bool _disposed;

    public DefaultStore(DefaultStoreOptions options)
    {
        _database = new DefaultDatabase(options.Path);
        _transactions = new TransactionCollection(_database.GetOrAdd("tx"));
        _blocks = new BlockCollection(_database.GetOrAdd("block"));
        _txExecutions = new TxExecutionCollection(_database.GetOrAdd("txexec"));
        _blockHashes = new BlockHashCollection(_database.GetOrAdd("txbindex"));
        _blockCommits = new BlockCommitCollection(_database.GetOrAdd("blockcommit"));
        _nextStateRootHashes = new StateRootHashCollection(_database.GetOrAdd("nextstateroothash"));
        _pendingEvidence = new EvidenceCollection(_database.GetOrAdd("evidencep"));
        _committedEvidence = new EvidenceCollection(_database.GetOrAdd("evidencec"));
        if (options.Path == string.Empty)
        {
            _db = new LiteDatabase(new MemoryStream(), disposeStream: true);
        }
        else
        {
            var connectionString = new ConnectionString
            {
                Filename = Path.Combine(options.Path, "index.ldb"),
                Journal = options.Journal,
                CacheSize = options.IndexCacheSize,
                Flush = options.Flush,
            };

            if (options.ReadOnly)
            {
                connectionString.Mode = FileMode.ReadOnly;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                Type.GetType("Mono.Runtime") is null)
            {
                // macOS + .NETCore doesn't support shared lock.
                connectionString.Mode = FileMode.Exclusive;
            }

            _db = new LiteDatabase(connectionString);
        }

        lock (_db.Mapper)
        {
            _db.Mapper.RegisterType(
                hash => ModelSerializer.SerializeToBytes(hash),
                b => ModelSerializer.DeserializeFromBytes<BlockHash>(b.AsBinary));
            _db.Mapper.RegisterType(
                hash => hash.Bytes.ToArray(),
                b => new HashDigest<SHA256>(b.AsBinary));
            _db.Mapper.RegisterType(
                txid => txid.Bytes.ToArray(),
                b => new TxId(b.AsBinary));
            _db.Mapper.RegisterType(
                address => address.ToByteArray(),
                b => new Address(b.AsBinary));
            _db.Mapper.RegisterType(
                commit => ModelSerializer.SerializeToBytes(commit),
                b => ModelSerializer.DeserializeFromBytes<BlockCommit>(b.AsBinary));
            _db.Mapper.RegisterType(
                evidence => ModelSerializer.SerializeToBytes(evidence),
                b => ModelSerializer.DeserializeFromBytes<EvidenceId>(b.AsBinary));
        }

        Options = options;
    }

    public DefaultStoreOptions Options { get; }

    public override IEnumerable<Guid> ListChainIds()
    {
        return _db.GetCollectionNames()
            .Where(name => name.StartsWith(IndexColPrefix))
            .Select(name => ParseChainId(name.Substring(IndexColPrefix.Length)));
    }

    public override void DeleteChainId(Guid chainId)
    {
        _db.DropCollection(IndexCollection(chainId).Name);
        _db.DropCollection(TxNonceCollection(chainId).Name);
        _db.DropCollection(CommitCollection(chainId).Name);
    }

    public override Guid GetCanonicalChainId()
    {
        LiteCollection<BsonDocument> collection = _db.GetCollection<BsonDocument>("canon");
        var docId = new BsonValue("canon");
        BsonDocument doc = collection.FindById(docId);
        if (doc is null)
        {
            return Guid.Empty;
        }

        return doc.TryGetValue("chainId", out BsonValue ns)
            ? new Guid(ns.AsBinary)
            : Guid.Empty;
    }

    public override void SetCanonicalChainId(Guid chainId)
    {
        LiteCollection<BsonDocument> collection = _db.GetCollection<BsonDocument>("canon");
        var docId = new BsonValue("canon");
        byte[] idBytes = chainId.ToByteArray();
        collection.Upsert(docId, new BsonDocument() { ["chainId"] = new BsonValue(idBytes) });
    }

    public override long CountIndex(Guid chainId)
    {
        return IndexCollection(chainId).Count();
    }

    public override IEnumerable<BlockHash> IterateIndexes(Guid chainId, int offset, int? limit)
    {
        return IndexCollection(chainId)
            .Find(Query.All(), offset, limit ?? int.MaxValue)
            .Select(i => i.Hash);
    }

    public override BlockHash GetBlockHash(Guid chainId, long height)
    {
        if (height < 0)
        {
            height += CountIndex(chainId);

            if (height < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Height is out of range.");
            }
        }

        HashDoc doc = IndexCollection(chainId).FindById(height + 1);
        return doc is { } d ? d.Hash : throw new ArgumentOutOfRangeException(
            nameof(height), "Height is out of range.");
    }

    public override long AppendIndex(Guid chainId, BlockHash hash)
    {
        return IndexCollection(chainId).Insert(new HashDoc { Hash = hash }) - 1;
    }

    public override void ForkBlockIndexes(
        Guid sourceChainId,
        Guid destinationChainId,
        BlockHash branchpoint)
    {
        LiteCollection<HashDoc> srcColl = IndexCollection(sourceChainId);
        LiteCollection<HashDoc> destColl = IndexCollection(destinationChainId);

        BlockHash? genesisHash = IterateIndexes(sourceChainId, 0, 1)
            .Cast<BlockHash?>()
            .FirstOrDefault();

        if (genesisHash is null || branchpoint.Equals(genesisHash))
        {
            return;
        }

        destColl.Delete(Query.All());
        destColl.InsertBulk(srcColl.FindAll().TakeWhile(i => !i.Hash.Equals(branchpoint)));

        AppendIndex(destinationChainId, branchpoint);
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
        LiteCollection<BsonDocument> collection = TxNonceCollection(chainId);
        foreach (BsonDocument doc in collection.FindAll())
        {
            if (doc.TryGetValue("_id", out BsonValue id) && id.IsBinary)
            {
                var address = new Address([.. id.AsBinary]);
                if (doc.TryGetValue("v", out BsonValue v) && v.IsInt64 && v.AsInt64 > 0)
                {
                    yield return new KeyValuePair<Address, long>(address, v.AsInt64);
                }
            }
        }
    }

    public override long GetTxNonce(Guid chainId, Address address)
    {
        LiteCollection<BsonDocument> collection = TxNonceCollection(chainId);
        var docId = new BsonValue(address.ToByteArray());
        BsonDocument doc = collection.FindById(docId);

        if (doc is null)
        {
            return 0;
        }

        return doc.TryGetValue("v", out BsonValue v) ? v.AsInt64 : 0;
    }

    public override void IncreaseTxNonce(Guid chainId, Address signer, long delta = 1)
    {
        long nextNonce = GetTxNonce(chainId, signer) + delta;
        LiteCollection<BsonDocument> collection = TxNonceCollection(chainId);
        var docId = new BsonValue(signer.ToByteArray());
        collection.Upsert(docId, new BsonDocument() { ["v"] = new BsonValue(nextNonce) });
    }

    public override void ForkTxNonces(Guid sourceChainId, Guid destinationChainId)
    {
        LiteCollection<BsonDocument> srcColl = TxNonceCollection(sourceChainId);
        LiteCollection<BsonDocument> destColl = TxNonceCollection(destinationChainId);
        destColl.InsertBulk(srcColl.FindAll());
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
        LiteCollection<BsonDocument> collection = CommitCollection(chainId);
        var docId = new BsonValue("c");
        BsonDocument doc = collection.FindById(docId);
        return doc is { } d && d.TryGetValue("v", out BsonValue v)
            ? ModelSerializer.DeserializeFromBytes<BlockCommit>(v.AsBinary)
            : BlockCommit.Empty;
    }

    public override void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit)
    {
        LiteCollection<BsonDocument> collection = CommitCollection(chainId);
        var docId = new BsonValue("c");
        BsonDocument doc = collection.FindById(docId);
        collection.Upsert(
            docId,
            new BsonDocument() { ["v"] = new BsonValue(ModelSerializer.SerializeToBytes(blockCommit)) });
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
            _db?.Dispose();
            // _root.Dispose();
            _disposed = true;
        }
    }

    internal static Guid ParseChainId(string chainIdString) =>
        new Guid(ByteUtility.ParseHex(chainIdString));

    internal static string FormatChainId(Guid chainId) =>
        ByteUtility.Hex(chainId.ToByteArray());

    [StoreLoader("default+file")]
    private static (IStore Store, TrieStateStore StateStore) Loader(Uri storeUri)
    {
        NameValueCollection query = HttpUtility.ParseQueryString(storeUri.Query);
        bool journal = query.GetBoolean("journal", true);
        int indexCacheSize = query.GetInt32("index-cache", 50000);
        int blockCacheSize = query.GetInt32("block-cache", 512);
        int txCacheSize = query.GetInt32("tx-cache", 1024);
        int evidenceCacheSize = query.GetInt32("evidence-cache", 1024);
        bool flush = query.GetBoolean("flush", true);
        bool readOnly = query.GetBoolean("readonly");
        string statesKvPath = query.Get("states-dir") ?? StatesKvPathDefault;
        var options = new DefaultStoreOptions
        {
            Path = storeUri.LocalPath,
            Journal = journal,
            IndexCacheSize = indexCacheSize,
            BlockCacheSize = blockCacheSize,
            TxCacheSize = txCacheSize,
            EvidenceCacheSize = evidenceCacheSize,
            Flush = flush,
            ReadOnly = readOnly,
        };

        var store = new DefaultStore(options);
        var stateStore = new TrieStateStore(
            new DefaultKeyValueStore(Path.Combine(storeUri.LocalPath, statesKvPath)));
        return (store, stateStore);
    }

    private LiteCollection<HashDoc> IndexCollection(in Guid chainId) =>
        _db.GetCollection<HashDoc>($"{IndexColPrefix}{FormatChainId(chainId)}");

    private LiteCollection<BsonDocument> TxNonceCollection(Guid chainId) =>
        _db.GetCollection<BsonDocument>($"{TxNonceIdPrefix}{FormatChainId(chainId)}");

    private LiteCollection<BsonDocument> CommitCollection(in Guid chainId) =>
        _db.GetCollection<BsonDocument>($"{CommitColPrefix}{FormatChainId(chainId)}");

    private class HashDoc
    {
        public long Id { get; set; }

        public BlockHash Hash { get; set; }
    }
}
