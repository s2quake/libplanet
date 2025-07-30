using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web;
using Bencodex.Types;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;
using LiteDB;
using LruCacheNet;
using Serilog;
using Zio;
using Zio.FileSystems;
using FileMode = LiteDB.FileMode;

namespace Libplanet.Store;

public class DefaultStore : StoreBase
{
    private const string IndexColPrefix = "index_";
    private const string TxNonceIdPrefix = "nonce_";
    private const string CommitColPrefix = "commit_";
    private const string StatesKvPathDefault = "states";

    private static readonly UPath TxRootPath = UPath.Root / "tx";
    private static readonly UPath BlockRootPath = UPath.Root / "block";
    private static readonly UPath TxExecutionRootPath = UPath.Root / "txexec";
    private static readonly UPath TxIdBlockHashRootPath = UPath.Root / "txbindex";
    private static readonly UPath BlockPerceptionRootPath = UPath.Root / "blockpercept";
    private static readonly UPath BlockCommitRootPath = UPath.Root / "blockcommit";
    private static readonly UPath NextStateRootHashRootPath = UPath.Root / "nextstateroothash";
    private static readonly UPath PendingEvidenceRootPath = UPath.Root / "evidencep";
    private static readonly UPath CommittedEvidenceRootPath = UPath.Root / "evidencec";
    private static readonly Codec Codec = new Codec();

    private readonly TransactionCollection _transactions;

    private readonly ILogger _logger;

    private readonly IFileSystem _root;
    // private readonly SubFileSystem _txs;
    private readonly SubFileSystem _blocks;
    private readonly SubFileSystem _txExecutions;
    private readonly SubFileSystem _txIdBlockHashIndex;
    private readonly SubFileSystem _blockPerceptions;
    private readonly SubFileSystem _blockCommits;
    private readonly SubFileSystem _nextStateRootHashes;
    private readonly SubFileSystem _pendingEvidence;
    private readonly SubFileSystem _committedEvidence;
    // private readonly LruCache<TxId, object> _txCache;
    private readonly LruCache<BlockHash, BlockDigest> _blockCache;
    private readonly LruCache<EvidenceId, EvidenceBase> _evidenceCache;

    private readonly LiteDatabase _db;

    private bool _disposed;

    public DefaultStore(DefaultStoreOptions options)
    {
        _logger = Log.ForContext<DefaultStore>();

        if (options.Path == string.Empty)
        {
            _transactions = new TransactionCollection(new DefaultKeyValueStore());
            _root = new MemoryFileSystem();
            _db = new LiteDatabase(new MemoryStream(), disposeStream: true);
        }
        else
        {
            if (!Path.IsPathFullyQualified(options.Path))
            {
                throw new ArgumentException(
                    "The path must be fully qualified.", nameof(options));
            }

            if (!Directory.Exists(options.Path))
            {
                Directory.CreateDirectory(options.Path);
            }

            _transactions = new TransactionCollection(new DefaultKeyValueStore(Path.Combine(options.Path, "tx")));

            var pfs = new PhysicalFileSystem();
            _root = new SubFileSystem(
                pfs,
                pfs.ConvertPathFromInternal(options.Path),
                owned: true);

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

        _root.CreateDirectory(TxRootPath);
        // _txs = new SubFileSystem(_root, TxRootPath, owned: false);
        _root.CreateDirectory(BlockRootPath);
        _blocks = new SubFileSystem(_root, BlockRootPath, owned: false);
        _root.CreateDirectory(TxExecutionRootPath);
        _txExecutions = new SubFileSystem(_root, TxExecutionRootPath, owned: false);
        _root.CreateDirectory(TxIdBlockHashRootPath);
        _txIdBlockHashIndex = new SubFileSystem(_root, TxIdBlockHashRootPath, owned: false);
        _root.CreateDirectory(BlockPerceptionRootPath);
        _blockPerceptions = new SubFileSystem(_root, BlockPerceptionRootPath, owned: false);
        _root.CreateDirectory(BlockCommitRootPath);
        _blockCommits = new SubFileSystem(_root, BlockCommitRootPath, owned: false);
        _root.CreateDirectory(NextStateRootHashRootPath);
        _nextStateRootHashes =
            new SubFileSystem(_root, NextStateRootHashRootPath, owned: false);
        _root.CreateDirectory(PendingEvidenceRootPath);
        _pendingEvidence = new SubFileSystem(_root, PendingEvidenceRootPath, owned: false);
        _root.CreateDirectory(CommittedEvidenceRootPath);
        _committedEvidence = new SubFileSystem(_root, CommittedEvidenceRootPath, owned: false);

        // _txCache = new LruCache<TxId, object>(capacity: options.TxCacheSize);
        _blockCache = new LruCache<BlockHash, BlockDigest>(capacity: options.BlockCacheSize);
        _evidenceCache = new LruCache<EvidenceId, EvidenceBase>(
            capacity: options.EvidenceCacheSize);
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc cref="StoreBase.IterateIndexes(Guid, int, int?)"/>
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

    /// <inheritdoc cref="StoreBase.AppendIndex(Guid, BlockHash)"/>
    public override long AppendIndex(Guid chainId, BlockHash hash)
    {
        return IndexCollection(chainId).Insert(new HashDoc { Hash = hash }) - 1;
    }

    /// <inheritdoc cref="StoreBase.ForkBlockIndexes(Guid, Guid, BlockHash)"/>
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

    public override Transaction? GetTransaction(TxId txId)
    {
        return _transactions[txId];
        // if (_txCache.TryGetValue(txid, out object cachedTx))
        // {
        //     return (Transaction)cachedTx;
        // }

        // UPath path = TxPath(txid);
        // if (!_txs.FileExists(path))
        // {
        //     return null;
        // }

        // byte[] bytes;
        // try
        // {
        //     bytes = _txs.ReadAllBytes(path);
        // }
        // catch (FileNotFoundException)
        // {
        //     return null;
        // }

        // var tx = ModelSerializer.DeserializeFromBytes<Transaction>(bytes);
        // _txCache.AddOrUpdate(txid, tx);
        // return tx;
    }

    public override void PutTransaction(Transaction tx)
    {
        // if (_txCache.ContainsKey(tx.Id))
        // {
        //     return;
        // }

        // WriteContentAddressableFile(_txs, TxPath(tx.Id), ModelSerializer.SerializeToBytes(tx));
        // _txCache.AddOrUpdate(tx.Id, tx);
        _transactions.Add(tx.Id, tx);
    }

    public override bool ContainsTransaction(TxId txId)
    {
        return _transactions.ContainsKey(txId);
        // if (_txCache.ContainsKey(txId))
        // {
        //     return true;
        // }

        // return _txs.FileExists(TxPath(txId));
    }

    /// <inheritdoc cref="StoreBase.IterateBlockHashes()"/>
    public override IEnumerable<BlockHash> IterateBlockHashes()
    {
        foreach (UPath path in _blocks.EnumerateDirectories(UPath.Root))
        {
            string upper = path.GetName();
            if (upper.Length != 2)
            {
                continue;
            }

            foreach (UPath subPath in _blocks.EnumerateFiles(path))
            {
                string lower = subPath.GetName();
                string name = upper + lower;
                BlockHash blockHash;
                try
                {
                    blockHash = BlockHash.Parse(name);
                }
                catch (Exception)
                {
                    // Skip if a filename does not match to the format.
                    continue;
                }

                yield return blockHash;
            }
        }
    }

    /// <inheritdoc cref="StoreBase.GetBlockDigest(BlockHash)"/>
    public override BlockDigest GetBlockDigest(BlockHash blockHash)
    {
        if (_blockCache.TryGetValue(blockHash, out BlockDigest cachedDigest))
        {
            return cachedDigest;
        }

        UPath path = BlockPath(blockHash);
        if (!_blocks.FileExists(path))
        {
            return default;
        }

        BlockDigest blockDigest;
        try
        {
            blockDigest = ModelSerializer.DeserializeFromBytes<BlockDigest>(_blocks.ReadAllBytes(path));
        }
        catch (FileNotFoundException)
        {
            return default;
        }

        _blockCache.AddOrUpdate(blockHash, blockDigest);
        return blockDigest;
    }

    public override void PutBlock(Block block)
    {
        if (_blockCache.ContainsKey(block.BlockHash))
        {
            return;
        }

        UPath path = BlockPath(block.BlockHash);
        if (_blocks.FileExists(path))
        {
            return;
        }

        foreach (Transaction tx in block.Transactions)
        {
            PutTransaction(tx);
        }

        BlockDigest digest = BlockDigest.Create(block);
        WriteContentAddressableFile(_blocks, path, ModelSerializer.SerializeToBytes(digest));
        _blockCache.AddOrUpdate(block.BlockHash, digest);
    }

    /// <inheritdoc cref="StoreBase.DeleteBlock(BlockHash)"/>
    public override bool DeleteBlock(BlockHash blockHash)
    {
        var path = BlockPath(blockHash);
        if (_blocks.FileExists(path))
        {
            _blocks.DeleteFile(path);
            _blockCache.Remove(blockHash);
            return true;
        }

        return false;
    }

    /// <inheritdoc cref="StoreBase.ContainsBlock(BlockHash)"/>
    public override bool ContainsBlock(BlockHash blockHash)
    {
        if (_blockCache.ContainsKey(blockHash))
        {
            return true;
        }

        UPath blockPath = BlockPath(blockHash);
        return _blocks.FileExists(blockPath);
    }

    /// <inheritdoc cref="StoreBase.PutTxExecution"/>
    public override void PutTxExecution(TxExecution txExecution)
    {
        UPath path = TxExecutionPath(txExecution);
        UPath dirPath = path.GetDirectory();
        CreateDirectoryRecursively(_txExecutions, dirPath);
        using Stream f =
            _txExecutions.OpenFile(path, System.IO.FileMode.Create, FileAccess.Write);
        Codec.Encode(SerializeTxExecution(txExecution), f);
    }

    /// <inheritdoc cref="StoreBase.GetTxExecution(BlockHash, TxId)"/>
    public override TxExecution? GetTxExecution(BlockHash blockHash, TxId txid)
    {
        UPath path = TxExecutionPath(blockHash, txid);
        if (_txExecutions.FileExists(path))
        {
            IValue decoded;
            using (Stream f = _txExecutions.OpenFile(
                path, System.IO.FileMode.Open, FileAccess.Read))
            {
                try
                {
                    decoded = Codec.Decode(f);
                }
                catch (DecodingException e)
                {
                    const string msg =
                        "Uncaught exception during " + nameof(GetTxExecution);
                    _logger.Error(e, msg);
                    return null;
                }
            }

            return DeserializeTxExecution(blockHash, txid, decoded, _logger);
        }

        return null;
    }

    /// <inheritdoc cref="StoreBase.PutTxIdBlockHashIndex(TxId, BlockHash)"/>
    public override void PutTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
    {
        var path = TxIdBlockHashIndexPath(txId, blockHash);
        var dirPath = path.GetDirectory();
        CreateDirectoryRecursively(_txIdBlockHashIndex, dirPath);
        _txIdBlockHashIndex.WriteAllBytes(path, blockHash.Bytes.ToArray());
    }

    public override IEnumerable<BlockHash> IterateTxIdBlockHashIndex(TxId txId)
    {
        var txPath = TxPath(txId);
        if (!_txIdBlockHashIndex.DirectoryExists(txPath))
        {
            yield break;
        }

        foreach (var path in _txIdBlockHashIndex.EnumerateFiles(txPath))
        {
            yield return new BlockHash(ByteUtility.ParseHex(path.GetName()));
        }
    }

    /// <inheritdoc cref="StoreBase.DeleteTxIdBlockHashIndex(TxId, BlockHash)"/>
    public override void DeleteTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
    {
        var path = TxIdBlockHashIndexPath(txId, blockHash);
        if (_txIdBlockHashIndex.FileExists(path))
        {
            _txIdBlockHashIndex.DeleteFile(path);
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

    /// <inheritdoc />
    public override BlockCommit GetChainBlockCommit(Guid chainId)
    {
        LiteCollection<BsonDocument> collection = CommitCollection(chainId);
        var docId = new BsonValue("c");
        BsonDocument doc = collection.FindById(docId);
        return doc is { } d && d.TryGetValue("v", out BsonValue v)
            ? ModelSerializer.DeserializeFromBytes<BlockCommit>(v.AsBinary)
            : BlockCommit.Empty;
    }

    /// <inheritdoc />
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
        UPath path = BlockCommitPath(blockHash);
        if (!_blockCommits.FileExists(path))
        {
            return default;
        }

        byte[] bytes;
        try
        {
            bytes = _blockCommits.ReadAllBytes(path);
        }
        catch (FileNotFoundException)
        {
            return default;
        }

        BlockCommit blockCommit = ModelSerializer.DeserializeFromBytes<BlockCommit>(bytes);
        return blockCommit;
    }

    /// <inheritdoc />
    public override void PutBlockCommit(BlockCommit blockCommit)
    {
        UPath path = BlockCommitPath(blockCommit.BlockHash);
        if (_blockCommits.FileExists(path))
        {
            return;
        }

        WriteContentAddressableFile(
            _blockCommits, path, ModelSerializer.SerializeToBytes(blockCommit));
    }

    /// <inheritdoc />
    public override void DeleteBlockCommit(BlockHash blockHash)
    {
        UPath path = BlockCommitPath(blockHash);
        if (!_blockCommits.FileExists(path))
        {
            return;
        }

        _blockCommits.DeleteFile(path);
    }

    public override IEnumerable<BlockHash> GetBlockCommitHashes()
    {
        var hashes = new List<BlockHash>();

        foreach (UPath path in _blockCommits.EnumerateFiles(UPath.Root))
        {
            if (path.FullName.Split('/').LastOrDefault() is { } name)
            {
                hashes.Add(new BlockHash(ByteUtility.ParseHex(name)));
            }
            else
            {
                throw new InvalidOperationException("Failed to get the block hash.");
            }
        }

        return hashes.AsEnumerable();
    }

    public override HashDigest<SHA256>? GetNextStateRootHash(BlockHash blockHash)
    {
        UPath path = NextStateRootHashPath(blockHash);
        if (!_nextStateRootHashes.FileExists(path))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = _nextStateRootHashes.ReadAllBytes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        HashDigest<SHA256> nextStateRootHash = new HashDigest<SHA256>(bytes);
        return nextStateRootHash;
    }

    public override void PutNextStateRootHash(
        BlockHash blockHash, HashDigest<SHA256> nextStateRootHash)
    {
        UPath path = NextStateRootHashPath(blockHash);
        if (_nextStateRootHashes.FileExists(path))
        {
            return;
        }

        WriteContentAddressableFile(
            _nextStateRootHashes, path, nextStateRootHash.Bytes.ToArray());
    }

    /// <inheritdoc />
    public override void DeleteNextStateRootHash(BlockHash blockHash)
    {
        UPath path = NextStateRootHashPath(blockHash);
        if (!_nextStateRootHashes.FileExists(path))
        {
            return;
        }

        _nextStateRootHashes.DeleteFile(path);
    }

    public override IEnumerable<EvidenceId> IteratePendingEvidenceIds()
    {
        foreach (UPath path in _pendingEvidence.EnumerateFiles(UPath.Root))
        {
            EvidenceId evidenceId;
            try
            {
                var name = path.FullName.Split('/').LastOrDefault() ?? string.Empty;
                evidenceId = EvidenceId.Parse(name);
            }
            catch (Exception)
            {
                // Skip if a filename does not match to the format.
                continue;
            }

            yield return evidenceId;
        }
    }

    public override EvidenceBase? GetPendingEvidence(EvidenceId evidenceId)
    {
        UPath path = PendingEvidencePath(evidenceId);
        if (!_pendingEvidence.FileExists(path))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = _pendingEvidence.ReadAllBytes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        try
        {
            EvidenceBase evidence = ModelSerializer.DeserializeFromBytes<EvidenceBase>(bytes);
            return evidence;
        }
        catch
        {
            return null;
        }
    }

    public override void PutPendingEvidence(EvidenceBase evidence)
    {
        if (_evidenceCache.ContainsKey(evidence.Id))
        {
            return;
        }

        if (_pendingEvidence.FileExists(PendingEvidencePath(evidence.Id)))
        {
            return;
        }

        WriteContentAddressableFile(
            _pendingEvidence,
            PendingEvidencePath(evidence.Id),
            ModelSerializer.SerializeToBytes(evidence));
    }

    public override void DeletePendingEvidence(EvidenceId evidenceId)
    {
        UPath path = PendingEvidencePath(evidenceId);
        if (!_pendingEvidence.FileExists(path))
        {
            return;
        }

        _pendingEvidence.DeleteFile(path);
    }

    public override bool ContainsPendingEvidence(EvidenceId evidenceId)
    {
        return _pendingEvidence.FileExists(PendingEvidencePath(evidenceId));
    }

    public override EvidenceBase? GetCommittedEvidence(EvidenceId evidenceId)
    {
        if (_evidenceCache.TryGetValue(evidenceId, out EvidenceBase cachedEvidence))
        {
            return cachedEvidence;
        }

        UPath path = CommittedEvidencePath(evidenceId);
        if (!_committedEvidence.FileExists(path))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = _committedEvidence.ReadAllBytes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        try
        {
            EvidenceBase evidence = ModelSerializer.DeserializeFromBytes<EvidenceBase>(bytes);
            return evidence;
        }
        catch
        {
            return null;
        }
    }

    public override void PutCommittedEvidence(EvidenceBase evidence)
    {
        if (_evidenceCache.ContainsKey(evidence.Id))
        {
            return;
        }

        if (_committedEvidence.FileExists(CommittedEvidencePath(evidence.Id)))
        {
            return;
        }

        WriteContentAddressableFile(
            _committedEvidence,
            CommittedEvidencePath(evidence.Id),
            ModelSerializer.SerializeToBytes(evidence));

        _evidenceCache.AddOrUpdate(evidence.Id, evidence);
    }

    public override void DeleteCommittedEvidence(EvidenceId evidenceId)
    {
        _evidenceCache.Remove(evidenceId);

        UPath path = CommittedEvidencePath(evidenceId);
        if (!_committedEvidence.FileExists(path))
        {
            return;
        }

        _committedEvidence.DeleteFile(path);
    }

    public override bool ContainsCommittedEvidence(EvidenceId evidenceId)
    {
        if (_evidenceCache.ContainsKey(evidenceId))
        {
            return true;
        }

        return _committedEvidence.FileExists(CommittedEvidencePath(evidenceId));
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
            _root.Dispose();
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

    private static void CreateDirectoryRecursively(IFileSystem fs, UPath path)
    {
        if (!fs.DirectoryExists(path))
        {
            CreateDirectoryRecursively(fs, path.GetDirectory());
            fs.CreateDirectory(path);
        }
    }

    private void WriteContentAddressableFile(IFileSystem fs, UPath path, byte[] contents)
    {
        UPath dirPath = path.GetDirectory();
        CreateDirectoryRecursively(fs, dirPath);

        // Assuming the filename is content-addressable, so that if there is
        // already the file of the same name the content is the same as well.
        if (fs.FileExists(path))
        {
            return;
        }

        // For atomicity, writes bytes into an intermediate temp file,
        // and then renames it to the final destination.
        UPath tmpPath = dirPath / $".{Guid.NewGuid():N}.tmp";
        try
        {
            fs.WriteAllBytes(tmpPath, contents);
            try
            {
                fs.MoveFile(tmpPath, path);
            }
            catch (IOException)
            {
                if (!fs.FileExists(path) ||
                    fs.GetFileLength(path) != contents.LongLength)
                {
                    throw;
                }
            }
        }
        finally
        {
            if (fs.FileExists(tmpPath))
            {
                fs.DeleteFile(tmpPath);
            }
        }
    }

    private UPath BlockPath(in BlockHash blockHash)
    {
        string idHex = ByteUtility.Hex(blockHash.Bytes);
        if (idHex.Length < 3)
        {
            throw new ArgumentException(
                $"Too short block hash: \"{idHex}\".",
                nameof(blockHash));
        }

        return UPath.Root / idHex.Substring(0, 2) / idHex.Substring(2);
    }

    private UPath TxPath(in TxId txid)
    {
        string idHex = txid.ToString();
        return UPath.Root / idHex.Substring(0, 2) / idHex.Substring(2);
    }

    private UPath BlockCommitPath(in BlockHash blockHash)
    {
        return UPath.Combine(UPath.Root, blockHash.ToString());
    }

    private UPath NextStateRootHashPath(in BlockHash blockHash)
    {
        return UPath.Root / blockHash.ToString();
    }

    private UPath PendingEvidencePath(in EvidenceId evidenceId)
    {
        return UPath.Combine(UPath.Root, evidenceId.ToString());
    }

    private UPath CommittedEvidencePath(in EvidenceId evidenceId)
    {
        return UPath.Combine(UPath.Root, evidenceId.ToString());
    }

    private UPath TxExecutionPath(in BlockHash blockHash, in TxId txid) =>
        BlockPath(blockHash) / txid.ToString();

    private UPath TxExecutionPath(TxExecution txExecution) =>
        TxExecutionPath(txExecution.BlockHash, txExecution.TxId);

    private UPath TxIdBlockHashIndexPath(in TxId txid, in BlockHash blockHash) =>
        TxPath(txid) / blockHash.ToString();

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
