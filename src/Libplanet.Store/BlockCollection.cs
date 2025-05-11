using System.Collections;
using System.Diagnostics.CodeAnalysis;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockCollection(Libplanet.Store.Store store, int cacheSize = 4096)
    : IReadOnlyDictionary<BlockHash, Block>
{
    private readonly Libplanet.Store.Store _store = store;
    private readonly ICache<BlockHash, Block> _cache = new ConcurrentLruBuilder<BlockHash, Block>()
            .WithCapacity(cacheSize)
            .Build();

    public IEnumerable<BlockHash> Keys => [.. _store.IterateBlockHashes()];

    public IEnumerable<Block> Values =>
        _store.IterateBlockHashes()
            .Select(GetBlock)
            .Where(block => block is { })
            .Select(block => block!)
            .ToList();

    public int Count => _store.BlockDigests.Count;

    public Block this[BlockHash blockHash]
    {
        get
        {
            Block? block = GetBlock(blockHash);
            if (block is null)
            {
                throw new KeyNotFoundException(
                    $"The given hash[{blockHash}] was not found in this set.");
            }

            if (!block.BlockHash.Equals(blockHash))
            {
                throw new InvalidOperationException(
                    $"The given hash[{blockHash}] was not equal to actual[{block.BlockHash}].");
            }

            return block;
        }

        set
        {
            if (!value.BlockHash.Equals(blockHash))
            {
                throw new InvalidOperationException(
                    $"{value}.hash does not match to {blockHash}");
            }

            value.Header.Timestamp.ValidateTimestamp();
            _store.PutBlock(value);
            _cache.AddOrUpdate(value.BlockHash, value);
        }
    }

    public bool Contains(KeyValuePair<BlockHash, Block> item) => _store.ContainsBlock(item.Key);

    public bool ContainsKey(BlockHash key) => _store.ContainsBlock(key);

    public bool Remove(BlockHash blockHash)
    {
        _store.BlockCommits.Remove(blockHash);
        _store.BlockDigests.Remove(blockHash);
        _cache.TryRemove(blockHash);

        return deleted;
    }

    public void Add(Block block)
    {
        _store.BlockDigests.Add(block.BlockHash, (BlockDigest)block);
    }

    public bool TryGetValue(BlockHash key, [MaybeNullWhen(false)] out Block value)
    {
        if (_cache.TryGet(key, out value))
        {
            return true;
        }

        if (_store.BlockDigests.TryGetValue(key, out var blockDigest))
        {
            value = blockDigest;
            _cache.AddOrUpdate(key, value);
            return true;
        }
    }

    public void Clear()
    {
        _cache.Clear();
        _store.BlockCommits.Clear();
    }

    public void CopyTo(KeyValuePair<BlockHash, Block>[] array, int arrayIndex)
    {
        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (Count > array.Length + arrayIndex)
        {
            var message = "The number of elements in the source BlockSet is greater than the " +
                          "available space from arrayIndex to the end of the destination array.";
            throw new ArgumentException(message, nameof(array));
        }

        foreach (KeyValuePair<BlockHash, Block> kv in this)
        {
            array[arrayIndex++] = kv;
        }
    }

    public IEnumerator<KeyValuePair<BlockHash, Block>> GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<BlockHash, Block>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Block GetBlock(BlockHash blockHash)
    {
        if (_cache.TryGet(blockHash, out var cached))
        {
            return cached;
        }

        var block = _store.GetBlock(blockHash);
        _cache.AddOrUpdate(blockHash, block);
        return block;
    }
}
