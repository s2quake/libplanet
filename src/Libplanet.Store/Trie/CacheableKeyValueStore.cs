using System;
using System.Collections.Generic;
using LruCacheNet;

namespace Libplanet.Store.Trie;

public sealed class CacheableKeyValueStore(IKeyValueStore keyValueStore, int cacheSize = 100)
    : IKeyValueStore, IDisposable
{
    private readonly LruCache<KeyBytes, byte[]> _cache = new(cacheSize);
    private bool _isDisposed;

    public IEnumerable<KeyBytes> Keys => keyValueStore.Keys;

    public byte[] this[in KeyBytes key]
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

    public void SetMany(IDictionary<KeyBytes, byte[]> values) => keyValueStore.SetMany(values);

    public bool Remove(in KeyBytes key)
    {
        if (keyValueStore.Remove(key))
        {
            _cache.Remove(key);
            return true;
        }

        return false;
    }

    public bool ContainsKey(in KeyBytes key)
        => _cache.ContainsKey(key) || keyValueStore.ContainsKey(key);

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
