using System.Collections;
using System.Diagnostics.CodeAnalysis;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Store;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public sealed class BlockCollection : IReadOnlyDictionary<BlockHash, Block>
{
    private readonly Repository _repository;
    private readonly Chain _chain;
    private readonly BlockDigestStore _blockDigests;
    private readonly BlockHashStore _blockHashes;
    private readonly ICache<BlockHash, Block> _cacheByHash;

    private readonly ICache<int, Block> _cacheByHeight;

    internal BlockCollection(Repository repository, int cacheSize = 4096)
    {
        _repository = repository;
        _chain = repository.Chain;
        _blockDigests = _repository.BlockDigests;
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
            var block = blockDigest.ToBlock(item => _repository.CommittedTransactions[item], item => _repository.CommittedEvidences[item]);
            _cacheByHash.AddOrUpdate(blockHash, block);
            return block;
        }
    }

    public IEnumerable<Block> this[Range range]
    {
        get
        {
            var start = range.Start.IsFromEnd ? Count - range.Start.Value : range.Start.Value;
            var end = range.End.IsFromEnd ? Count - range.End.Value : range.End.Value;

            if (start < 0 || end > Count || start > end)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            for (var i = start; i < end; i++)
            {
                yield return this[i];
            }
        }
    }

    public IEnumerable<BlockHash> IterateIndexes(int offset = 0, int? limit = null)
    {
        return _blockHashes.IterateHeights(offset, limit);
    }

    public bool ContainsKey(BlockHash blockHash) => _blockDigests.ContainsKey(blockHash);

    public bool Remove(BlockHash blockHash)
    {
        if (_blockDigests.TryGetValue(blockHash, out var blockDigest))
        {
            _blockDigests.Remove(blockHash);
            _blockHashes.Remove(blockDigest.Height);
            _repository.PendingTransactions.RemoveRange(blockDigest.TxIds);
            _repository.CommittedEvidences.RemoveRange(blockDigest.EvidenceIds);
            _cacheByHash.TryRemove(blockHash);
            _cacheByHeight.TryRemove(blockDigest.Height);
            return true;
        }

        return false;
    }

    internal void AddCache(Block block)
    {
        // _repository.AddBlock(block);
        // _chain.BlockHashes.Add(block);

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
            value = blockDigest.ToBlock(item => _repository.PendingTransactions[item], item => _repository.CommittedEvidences[item]);
            _cacheByHash.AddOrUpdate(blockHash, value);
            return true;
        }

        return false;
    }

    // public void Clear()
    // {
    //     _cacheByHash.Clear();
    //     _blockDigests.Clear();
    // }

    public IEnumerator<KeyValuePair<BlockHash, Block>> GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<BlockHash, Block>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
