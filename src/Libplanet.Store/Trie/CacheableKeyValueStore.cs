using System.Diagnostics.CodeAnalysis;
using LruCacheNet;

namespace Libplanet.Store.Trie;

public sealed class CacheableKeyValueStore(IDictionary<KeyBytes, byte[]> keyValueStore, int cacheSize = 100)
    : KeyValueStoreBase, IDisposable
{
    private readonly LruCache<KeyBytes, byte[]> _cache = new(cacheSize);
    private bool _isDisposed;

    public override byte[] this[KeyBytes key]
    {
        get
        {
            if (_cache.TryGetValue(key, out var value) && value is { } v)
            {
                return v;
            }

            if (keyValueStore[key] is { } bytes)
            {
                _cache[key] = bytes;
                return bytes;
            }

            throw new KeyNotFoundException($"No such key: ${key}.");
        }

        set
        {
            keyValueStore[key] = value;
            _cache[key] = value;
        }
    }

    public override bool Remove(KeyBytes key)
    {
        if (keyValueStore.Remove(key))
        {
            _cache.Remove(key);
            return true;
        }

        return false;
    }

    public override bool ContainsKey(KeyBytes key) => _cache.ContainsKey(key) || keyValueStore.ContainsKey(key);

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public override void Add(KeyBytes key, byte[] value)
    {
        keyValueStore.Add(key, value);
        _cache[key] = value;
    }

    public override bool TryGetValue(KeyBytes key, [MaybeNullWhen(false)] out byte[] value)
    {
        if (_cache.TryGetValue(key, out var v) && v is { })
        {
            value = v;
            return true;
        }

        if (keyValueStore.TryGetValue(key, out var bytes) && bytes is { })
        {
            _cache[key] = bytes;
            value = bytes;
            return true;
        }

        value = null;
        return false;
    }

    public override void Clear()
    {
        _cache.Clear();
        keyValueStore.Clear();
    }

    protected override IEnumerable<KeyBytes> EnumerateKeys() => keyValueStore.Keys;
}
