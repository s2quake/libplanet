using System.Collections;
using System.Diagnostics.CodeAnalysis;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockCollection(Store store, int cacheSize = 4096)
    : IReadOnlyDictionary<BlockHash, Block>
{
    private readonly Store _store = store;
    private readonly ICache<BlockHash, Block> _cacheByHash = new ConcurrentLruBuilder<BlockHash, Block>()
            .WithCapacity(cacheSize)
            .Build();

    private readonly ICache<int, Block> _cacheByHeight = new ConcurrentLruBuilder<int, Block>()
            .WithCapacity(cacheSize)
            .Build();

    public IEnumerable<BlockHash> Keys => _store.BlockDigests.Keys;

    public IEnumerable<Block> Values => _store.BlockDigests.Values
            .Select(blockDigest => blockDigest.ToBlock(
                item => _store.Transactions[item],
                item => _store.CommittedEvidences[item]));

    public int Count => _store.BlockDigests.Count;

    public Block this[int height]
    {
        get
        {
            if (_cacheByHeight.TryGet(height, out var cached))
            {
                return cached;
            }

            var blockHash = _store.BlockHashes[height];
            var block = this[blockHash];
            _cacheByHeight.AddOrUpdate(height, block);
            return block;
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

            var blockDigest = _store.BlockDigests[blockHash];
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

    public bool ContainsKey(BlockHash blockHash) => _store.BlockDigests.ContainsKey(blockHash);

    public bool Remove(BlockHash blockHash)
    {
        if (_store.BlockDigests.Remove(blockHash))
        {
            _store.BlockCommits.Remove(blockHash);
            _cacheByHash.TryRemove(blockHash);
            _cacheByHeight.TryRemove(_store.BlockHashes.IndexOf(blockHash));
            return true;
        }

        return false;
    }

    public void Add(Block block)
    {
        _store.BlockDigests.Add(block);
        _store.BlockHashes.Add(block);
        _store.Transactions.Add(block);
        _store.PendingEvidences.Add(block);
        _store.CommittedEvidences.Add(block);

        _cacheByHash.AddOrUpdate(block.BlockHash, block);
        _cacheByHeight.AddOrUpdate(block.Height, block);
    }

    public bool TryGetValue(BlockHash key, [MaybeNullWhen(false)] out Block value)
    {
        if (_cacheByHash.TryGet(key, out value))
        {
            return true;
        }

        if (_store.BlockDigests.TryGetValue(key, out var blockDigest))
        {
            value = blockDigest.ToBlock(item => _store.Transactions[item], item => _store.CommittedEvidences[item]);
            _cacheByHash.AddOrUpdate(key, value);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        _cacheByHash.Clear();
        _store.BlockDigests.Clear();
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
