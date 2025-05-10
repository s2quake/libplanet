using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Store;

public abstract class Database<TTable> : IDatabase
    where TTable : IKeyValueStore
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, TTable> _tableByKey = new();
    private bool _disposedValue;

    public IEnumerable<string> Keys => _tableByKey.Keys;

    public IEnumerable<TTable> Values => _tableByKey.Values;

    public int Count => _tableByKey.Count;

    IEnumerable<IKeyValueStore> IReadOnlyDictionary<string, IKeyValueStore>.Values
    {
        get
        {
            foreach (var value in _tableByKey.Values)
            {
                yield return value;
            }
        }
    }

    IKeyValueStore IReadOnlyDictionary<string, IKeyValueStore>.this[string key] => _tableByKey[key];

    public TTable this[string key] => _tableByKey[key];

    public TTable GetOrAdd(string key)
    {
        lock (_lock)
        {
            return _tableByKey.GetOrAdd(key, (k, _) => Create(k), string.Empty);
        }
    }

    public bool ContainsKey(string key) => _tableByKey.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out TTable value)
        => _tableByKey.TryGetValue(key, out value);

    public bool Remove(string key)
    {
        lock (_lock)
        {
            if (_tableByKey.TryRemove(key, out var value))
            {
                OnRemove(key, value);
                return true;
            }

            return false;
        }
    }

    public IEnumerator<KeyValuePair<string, TTable>> GetEnumerator()
        => _tableByKey.GetEnumerator();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    IEnumerator IEnumerable.GetEnumerator() => _tableByKey.GetEnumerator();

    IKeyValueStore IDatabase.GetOrAdd(string key) => GetOrAdd(key);

    bool IReadOnlyDictionary<string, IKeyValueStore>.TryGetValue(string key, out IKeyValueStore value)
    {
        if (_tableByKey.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }

        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<string, IKeyValueStore>> IEnumerable<KeyValuePair<string, IKeyValueStore>>.GetEnumerator()
    {
        foreach (var kvp in _tableByKey)
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
