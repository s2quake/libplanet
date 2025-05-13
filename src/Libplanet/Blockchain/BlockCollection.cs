using System.Collections;
using System.Diagnostics.CodeAnalysis;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Store;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public sealed class BlockCollection : IReadOnlyDictionary<BlockHash, Block>
{
    private readonly Libplanet.Store.Store _store;
    private readonly Chain _chain;
    private readonly BlockDigestStore _blockDigests;
    private readonly BlockHashStore _blockHashes;
    private readonly ICache<BlockHash, Block> _cacheByHash;

    private readonly ICache<int, Block> _cacheByHeight;

    internal BlockCollection(Libplanet.Store.Store store, Guid chainId, int cacheSize = 4096)
    {
        _store = store;
        _chain = store.Chains.GetOrAdd(chainId);
        _blockDigests = _store.BlockDigests;
        _blockHashes = _chain.BlockHashes;
        _cacheByHash = new ConcurrentLruBuilder<BlockHash, Block>()
            .WithCapacity(cacheSize)
            .Build();
        _cacheByHeight = new ConcurrentLruBuilder<int, Block>()
            .WithCapacity(cacheSize)
            .Build();
    }

    public IEnumerable<BlockHash> Keys
    {
        get
        {
            for (var i = 0; i < _blockHashes.Count; i++)
            {
                yield return _blockHashes[i];
            }
        }
    }

    public IEnumerable<Block> Values
    {
        get
        {
            for (var i = 0; i < _blockHashes.Count; i++)
            {
                var blockHash = _blockHashes[i];
                yield return this[blockHash];
            }
        }
    }

    public int Count => _blockHashes.Count;

    public Block this[int height]
    {
        get
        {
            if (_cacheByHeight.TryGet(height, out var cached))
            {
                return cached;
            }

            var blockHash = _blockHashes[height];
            var block = this[blockHash];
            _cacheByHeight.AddOrUpdate(height, block);
            return block;
        }
    }

    public Block this[Index index]
    {
        get
        {
            if (index.IsFromEnd)
            {
                return this[Count - index.Value];
            }

            if (index.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return this[index.Value];
        }
    }

    public Block this[BlockHash blockHash]
    {
        get
        {
            if (_cacheByHash.TryGet(blockHash, out var cached))
            {
                return cached;
            }

            var blockDigest = _blockDigests[blockHash];
            var block = blockDigest.ToBlock(item => _store.Transactions[item], item => _store.CommittedEvidences[item]);
            _cacheByHash.AddOrUpdate(blockHash, block);
            return block;
        }

        set
        {
            if (value.BlockHash != blockHash)
            {
                throw new ArgumentException(
                    $"The block hash of the value ({value.BlockHash}) does not match the key ({blockHash}).");
            }

            Add(value);
        }
    }

    public IEnumerable<BlockHash> IterateIndexes(int offset = 0, int? limit = null)
    {
        return _blockHashes.IterateIndexes(offset, limit);
    }

    public bool ContainsKey(BlockHash blockHash) => _blockDigests.ContainsKey(blockHash);

    public bool Remove(BlockHash blockHash)
    {
        if (_blockDigests.TryGetValue(blockHash, out var blockDigest))
        {
            _blockDigests.Remove(blockHash);
            _blockHashes.Remove(blockDigest.Height);
            _store.Transactions.RemoveRange(blockDigest.TxIds);
            _store.CommittedEvidences.RemoveRange(blockDigest.EvidenceIds);
            _cacheByHash.TryRemove(blockHash);
            _cacheByHeight.TryRemove(blockDigest.Height);
            return true;
        }

        return false;
    }

    public void Add(Block block)
    {
        _blockDigests.Add(block);
        _blockHashes.Add(block);
        _store.Transactions.Add(block);
        _store.PendingEvidences.Add(block);
        _store.CommittedEvidences.Add(block);

        _cacheByHash.AddOrUpdate(block.BlockHash, block);
        _cacheByHeight.AddOrUpdate(block.Height, block);
    }

    public bool TryGetValue(int height, [MaybeNullWhen(false)] out Block value)
    {
        if (_cacheByHeight.TryGet(height, out value))
        {
            return true;
        }

        if (_blockHashes.TryGetValue(height, out var blockHash))
        {
            value = this[blockHash];
            _cacheByHeight.AddOrUpdate(height, value);
            return true;
        }

        return false;
    }

    public bool TryGetValue(BlockHash blockHash, [MaybeNullWhen(false)] out Block value)
    {
        if (_cacheByHash.TryGet(blockHash, out value))
        {
            return true;
        }

        if (_blockDigests.TryGetValue(blockHash, out var blockDigest))
        {
            value = blockDigest.ToBlock(item => _store.Transactions[item], item => _store.CommittedEvidences[item]);
            _cacheByHash.AddOrUpdate(blockHash, value);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        _cacheByHash.Clear();
        _blockDigests.Clear();
    }

    public IEnumerator<KeyValuePair<BlockHash, Block>> GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<BlockHash, Block>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
