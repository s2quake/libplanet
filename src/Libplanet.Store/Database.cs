using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Store;

public abstract class Database<TTable> : IDatabase
    where TTable : IKeyValueStore
{
    private readonly ConcurrentDictionary<string, TTable> _collectionByKey = new();
    private bool _disposedValue;

    public IEnumerable<string> Keys => _collectionByKey.Keys;

    public IEnumerable<TTable> Values => _collectionByKey.Values;

    public int Count => _collectionByKey.Count;

    IEnumerable<IKeyValueStore> IReadOnlyDictionary<string, IKeyValueStore>.Values
    {
        get
        {
            foreach (var value in _collectionByKey.Values)
            {
                yield return value;
            }
        }
    }

    IKeyValueStore IReadOnlyDictionary<string, IKeyValueStore>.this[string key]
        => _collectionByKey[key];

    public TTable this[string key] => _collectionByKey[key];

    public TTable GetOrAdd(string key) => _collectionByKey.GetOrAdd(key, Create);

    public bool ContainsKey(string key) => _collectionByKey.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out TTable value)
        => _collectionByKey.TryGetValue(key, out value);

    public bool Remove(string key)
    {
        if (_collectionByKey.TryRemove(key, out var value))
        {
            OnRemove(key, value);
            return true;
        }

        return false;
    }

    public IEnumerator<KeyValuePair<string, TTable>> GetEnumerator()
        => _collectionByKey.GetEnumerator();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    IEnumerator IEnumerable.GetEnumerator() => _collectionByKey.GetEnumerator();

    IKeyValueStore IDatabase.GetOrAdd(string key) => GetOrAdd(key);

    bool IReadOnlyDictionary<string, IKeyValueStore>.TryGetValue(string key, out IKeyValueStore value)
    {
        if (_collectionByKey.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }

        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<string, IKeyValueStore>> IEnumerable<KeyValuePair<string, IKeyValueStore>>.GetEnumerator()
    {
        foreach (var kvp in _collectionByKey)
        {
            yield return new KeyValuePair<string, IKeyValueStore>(kvp.Key, kvp.Value);
        }
    }

    protected abstract TTable Create(string key);

    protected virtual void OnRemove(string key, TTable value)
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Do nothing here.
            }

            _disposedValue = true;
        }
    }
}
