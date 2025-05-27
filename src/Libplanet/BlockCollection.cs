using System.Collections;
using System.Diagnostics.CodeAnalysis;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Data;
using Libplanet.Types.Blocks;

namespace Libplanet;

public sealed class BlockCollection : IReadOnlyDictionary<BlockHash, Block>
{
    private readonly Repository _repository;
    private readonly BlockDigestIndex _blockDigests;
    private readonly BlockHashIndex _blockHashes;
    private readonly ICache<BlockHash, Block> _cacheByHash;

    private readonly ICache<int, Block> _cacheByHeight;

    internal BlockCollection(Repository repository, int cacheSize = 4096)
    {
        _repository = repository;
        _blockDigests = _repository.BlockDigests;
        _blockHashes = _repository.BlockHashes;
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
            foreach (var item in _blockHashes[range])
            {
                yield return this[item];
            }
        }
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

    public IEnumerator<KeyValuePair<BlockHash, Block>> GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<BlockHash, Block>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
