using System;
using System.Collections.Generic;
using LruCacheNet;

namespace Libplanet.Store.Trie;

public sealed class CacheableKeyValueStore(IKeyValueStore keyValueStore, int cacheSize = 100)
    : IKeyValueStore, IDisposable
{
    private readonly LruCache<KeyBytes, byte[]> _cache = new(cacheSize);
    private bool _disposed;

    public byte[] this[in KeyBytes key]
    {
        get
        {
            if (_cache.TryGetValue(key, out byte[]? value) && value is { } v)
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

    public void Set(IDictionary<KeyBytes, byte[]> values)
    {
        keyValueStore.Set(values);
    }

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

    public IEnumerable<KeyBytes> ListKeys() => keyValueStore.ListKeys();

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
