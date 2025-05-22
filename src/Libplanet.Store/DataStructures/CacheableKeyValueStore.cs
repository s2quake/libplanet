using System.Diagnostics.CodeAnalysis;
using BitFaster.Caching;
using BitFaster.Caching.Lru;

namespace Libplanet.Store.DataStructures;

public sealed class CacheableKeyValueStore(IDictionary<string, byte[]> keyValueStore, int cacheSize = 100)
    : TableBase, IDisposable
{
    private readonly ICache<string, byte[]> _cache = new ConcurrentLruBuilder<string, byte[]>()
        .WithCapacity(cacheSize)
        .Build();

    private bool _isDisposed;

    public override int Count => keyValueStore.Count;

    public override byte[] this[string key]
    {
        get
        {
            if (_cache.TryGet(key, out var value) && value is { } v)
            {
                return v;
            }

            if (keyValueStore[key] is { } bytes)
            {
                _cache.AddOrUpdate(key, bytes);
                return bytes;
            }

            throw new KeyNotFoundException($"No such key: ${key}.");
        }

        set
        {
            keyValueStore[key] = value;
            _cache.AddOrUpdate(key, value);
        }
    }

    public override bool Remove(string key)
    {
        if (keyValueStore.Remove(key))
        {
            _cache.TryRemove(key);
            return true;
        }

        return false;
    }

    public override bool ContainsKey(string key) => _cache.TryGet(key, out _) || keyValueStore.ContainsKey(key);

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public override void Add(string key, byte[] value)
    {
        keyValueStore.Add(key, value);
        _cache.AddOrUpdate(key, value);
    }

    public override bool TryGetValue(string key, [MaybeNullWhen(false)] out byte[] value)
    {
        if (_cache.TryGet(key, out var v) && v is { })
        {
            value = v;
            return true;
        }

        if (keyValueStore.TryGetValue(key, out var bytes) && bytes is { })
        {
            _cache.AddOrUpdate(key, bytes);
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

    protected override IEnumerable<string> EnumerateKeys() => keyValueStore.Keys;
}
