using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Web;
using ImmutableTrie;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

/// <summary>
/// Volatile in-memory store.
/// <para>It is useful for storing temporal small chains, e.g., fixtures for unit tests of
/// game logic.</para>
/// <para><see cref="MemoryStore"/> and <see cref="MemoryKeyValueStore"/>-backed
/// <see cref="TrieStateStore"/> can be instantiated from a URI with <c>memory:</c> scheme
/// using <see cref="StoreLoaderAttribute.LoadStore(Uri)"/>, e.g.:</para>
/// <list type="bullet">
/// <item><description><c>memory:</c></description></item>
/// </list>
/// </summary>
public sealed class MemoryStore : IStore
{
    private readonly ConcurrentDictionary<Guid, ImmutableTrieList<BlockHash>> _indexes = new();
    private readonly ConcurrentDictionary<BlockHash, BlockDigest> _blocks = new();
    private readonly ConcurrentDictionary<TxId, Transaction> _txs = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Address, long>> _txNonces =
        new();

    private readonly ConcurrentDictionary<(BlockHash, TxId), TxExecution> _txExecutions = new();
    private readonly ConcurrentDictionary<TxId, ImmutableHashSet<BlockHash>> _txBlockIndexes =
        new();

    private readonly ConcurrentDictionary<BlockHash, BlockCommit> _blockCommits = new();
    private readonly ConcurrentDictionary<Guid, BlockCommit> _chainCommits = new();
    private readonly ConcurrentDictionary<EvidenceId, EvidenceBase> _pendingEvidence = new();
    private readonly ConcurrentDictionary<EvidenceId, EvidenceBase> _committedEvidence = new();

    private Guid? _canonicalChainId;

    void IDisposable.Dispose()
    {
        // Do nothing.
    }

    IEnumerable<Guid> IStore.ListChainIds() => _indexes.Keys;

    void IStore.DeleteChainId(Guid chainId)
    {
        _indexes.TryRemove(chainId, out _);
        _txNonces.TryRemove(chainId, out _);
    }

    Guid? IStore.GetCanonicalChainId() => _canonicalChainId;

    void IStore.SetCanonicalChainId(Guid chainId) => _canonicalChainId = chainId;

    long IStore.CountIndex(Guid chainId) =>
        _indexes.TryGetValue(chainId, out ImmutableTrieList<BlockHash>? index)
        ? index.Count
        : 0;

    IEnumerable<BlockHash> IStore.IterateIndexes(Guid chainId, int offset, int? limit)
    {
        if (_indexes.TryGetValue(chainId, out var list))
        {
            IEnumerable<BlockHash> index = list.Skip(offset);
            return limit is { } l ? index.Take(l) : index;
        }

        return [];
    }

    BlockHash? IStore.IndexBlockHash(Guid chainId, long index)
    {
        if (_indexes.TryGetValue(chainId, out var list))
        {
            if (index < 0)
            {
                index += list.Count;
            }

            if (index < list.Count && index >= 0)
            {
                return list[(int)index];
            }
        }

        return null;
    }

    long IStore.AppendIndex(Guid chainId, BlockHash hash)
    {
        ImmutableTrieList<BlockHash> list = _indexes.AddOrUpdate(
            chainId,
            _ => ImmutableTrieList.Create(hash),
            (_, list) => list.Add(hash)
        );
        _txNonces.GetOrAdd(chainId, _ => new ConcurrentDictionary<Address, long>());

        return list.Count - 1;
    }

    public void ForkBlockIndexes(
        Guid sourceChainId,
        Guid destinationChainId,
        BlockHash branchpoint
    )
    {
        if (_indexes.TryGetValue(sourceChainId, out ImmutableTrieList<BlockHash>? source))
        {
            int bpIndex = source.FindIndex(branchpoint.Equals);
            _indexes[destinationChainId] = source.GetRange(0, bpIndex + 1);
        }
    }

    Transaction? IStore.GetTransaction(TxId txid) =>
        _txs.TryGetValue(txid, out Transaction? untyped) && untyped is Transaction tx
            ? tx
            : null;

    void IStore.PutTransaction(Transaction tx) => _txs[tx.Id] = tx;

    IEnumerable<BlockHash> IStore.IterateBlockHashes() => _blocks.Keys;

    Block? IStore.GetBlock(BlockHash blockHash)
    {
        if (!_blocks.TryGetValue(blockHash, out BlockDigest digest))
        {
            return null;
        }

        BlockHeader header = digest.Header;
        ImmutableArray<TxId> txids = digest.TxIds.ToImmutableArray();
        IEnumerable<Transaction> txs = txids.Select(txid => _txs[txid]);
        ImmutableArray<EvidenceId> evidenceIds = digest.EvidenceIds.ToImmutableArray();
        IEnumerable<EvidenceBase> evidence
            = evidenceIds.Select(evId => _committedEvidence[evId]);
        return Block.Create(header, [.. txs], [.. evidence]);
    }

    long? IStore.GetBlockIndex(BlockHash blockHash) =>
        _blocks.TryGetValue(blockHash, out BlockDigest digest) ? digest.Height : null;

    BlockDigest IStore.GetBlockDigest(BlockHash blockHash) =>
        _blocks.TryGetValue(blockHash, out BlockDigest digest) ? digest : default;

    void IStore.PutBlock(Block block)
    {
        IReadOnlyList<Transaction> txs = block.Transactions;
        foreach (Transaction tx in txs)
        {
            _txs[tx.Id] = tx;
        }

        var evidence = block.Evidence;
        foreach (var ev in evidence)
        {
            _committedEvidence[ev.Id] = ev;
        }

        _blocks[block.Hash] = new BlockDigest
        {
            Header = block.Header,
            TxIds = [.. txs.Select(tx => tx.Id)],
            EvidenceIds = [.. evidence.Select(ev => ev.Id)],
        };
    }

    bool IStore.DeleteBlock(BlockHash blockHash) => _blocks.TryRemove(blockHash, out _);

    bool IStore.ContainsBlock(BlockHash blockHash) => _blocks.ContainsKey(blockHash);

    void IStore.PutTxExecution(TxExecution txExecution) =>
        _txExecutions[(txExecution.BlockHash, txExecution.TxId)] = txExecution;

    TxExecution? IStore.GetTxExecution(BlockHash blockHash, TxId txid) =>
        _txExecutions.TryGetValue((blockHash, txid), out TxExecution? e) ? e : null;

    void IStore.PutTxIdBlockHashIndex(TxId txId, BlockHash blockHash) =>
        _txBlockIndexes.AddOrUpdate(
            txId,
            _ => ImmutableHashSet.Create(blockHash),
            (_, set) => set.Add(blockHash)
        );

    BlockHash? IStore.GetFirstTxIdBlockHashIndex(TxId txId) =>
        _txBlockIndexes.TryGetValue(txId, out ImmutableHashSet<BlockHash>? set) && set.Any()
            ? set.First()
            : null;

    IEnumerable<BlockHash> IStore.IterateTxIdBlockHashIndex(TxId txId) =>
        _txBlockIndexes.TryGetValue(txId, out ImmutableHashSet<BlockHash>? set)
            ? set
            : Enumerable.Empty<BlockHash>();

    void IStore.DeleteTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
    {
        while (_txBlockIndexes.TryGetValue(txId, out ImmutableHashSet<BlockHash>? set) &&
               set.Contains(blockHash))
        {
            var removed = set.Remove(blockHash);
            _txBlockIndexes.TryUpdate(txId, removed, set);
        }
    }

    IEnumerable<KeyValuePair<Address, long>> IStore.ListTxNonces(Guid chainId) =>
        _txNonces.TryGetValue(chainId, out ConcurrentDictionary<Address, long>? dict)
            ? dict
            : Enumerable.Empty<KeyValuePair<Address, long>>();

    long IStore.GetTxNonce(Guid chainId, Address address) =>
        _txNonces.TryGetValue(chainId, out ConcurrentDictionary<Address, long>? dict) &&
        dict.TryGetValue(address, out long nonce)
            ? nonce
            : 0;

    void IStore.IncreaseTxNonce(Guid chainId, Address signer, long delta)
    {
        ConcurrentDictionary<Address, long> dict =
            _txNonces.GetOrAdd(chainId, _ => new ConcurrentDictionary<Address, long>());
        dict.AddOrUpdate(signer, _ => delta, (_, nonce) => nonce + delta);
    }

    bool IStore.ContainsTransaction(TxId txId) => _txs.ContainsKey(txId);

    long IStore.CountBlocks() => _blocks.Count;

    void IStore.ForkTxNonces(Guid sourceChainId, Guid destinationChainId)
    {
        if (_txNonces.TryGetValue(sourceChainId, out ConcurrentDictionary<Address, long>? dict))
        {
            _txNonces[destinationChainId] = new ConcurrentDictionary<Address, long>(dict);
        }
    }

    void IStore.PruneOutdatedChains(bool noopWithoutCanon)
    {
        if (!(_canonicalChainId is { } ccid))
        {
            if (noopWithoutCanon)
            {
                return;
            }

            throw new InvalidOperationException("Canonical chain ID is not assigned.");
        }

        foreach (Guid id in _indexes.Keys.Where(id => !id.Equals(ccid)))
        {
            ((IStore)this).DeleteChainId(id);
        }
    }

    public BlockCommit GetChainBlockCommit(Guid chainId) =>
        _chainCommits.TryGetValue(chainId, out var commit)
            ? commit
            : default;

    public void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit) =>
        _chainCommits[chainId] = blockCommit;

    public BlockCommit GetBlockCommit(BlockHash blockHash) =>
        _blockCommits.TryGetValue(blockHash, out var commit)
            ? commit
            : default;

    public void PutBlockCommit(BlockCommit blockCommit) =>
        _blockCommits[blockCommit.BlockHash] = blockCommit;

    public void DeleteBlockCommit(BlockHash blockHash) =>
        _blockCommits.TryRemove(blockHash, out _);

    public IEnumerable<BlockHash> GetBlockCommitHashes()
        => _blockCommits.Keys;

    public IEnumerable<EvidenceId> IteratePendingEvidenceIds()
        => _pendingEvidence.Keys;

    public EvidenceBase? GetPendingEvidence(EvidenceId evidenceId)
        => _pendingEvidence.TryGetValue(evidenceId, out var evidence)
        ? evidence
        : null;

    public void PutPendingEvidence(EvidenceBase evidence)
        => _pendingEvidence[evidence.Id] = evidence;

    public void DeletePendingEvidence(EvidenceId evidenceId)
        => _pendingEvidence.TryRemove(evidenceId, out _);

    public bool ContainsPendingEvidence(EvidenceId evidenceId)
        => _pendingEvidence.ContainsKey(evidenceId);

    public EvidenceBase? GetCommittedEvidence(EvidenceId evidenceId)
        => _committedEvidence.TryGetValue(evidenceId, out var evidence)
        ? evidence
        : null;

    public void PutCommittedEvidence(EvidenceBase evidence)
        => _committedEvidence[evidence.Id] = evidence;

    public void DeleteCommittedEvidence(EvidenceId evidenceId)
        => _committedEvidence.TryRemove(evidenceId, out _);

    public bool ContainsCommittedEvidence(EvidenceId evidenceId)
        => _committedEvidence.ContainsKey(evidenceId);

    [StoreLoader("memory")]
    private static (IStore Store, IStateStore StateStore) Loader(Uri storeUri)
    {
        NameValueCollection query = HttpUtility.ParseQueryString(storeUri.Query);
        var store = new MemoryStore();
        var stateStore = new TrieStateStore();
        return (store, stateStore);
    }
}
