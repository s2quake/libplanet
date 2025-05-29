using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Data;

public abstract class Database<TTable> : IDatabase
    where TTable : ITable
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, TTable> _tableByKey = new();

    public IEnumerable<string> Keys => _tableByKey.Keys;

    public IEnumerable<TTable> Values => _tableByKey.Values;

    public int Count => _tableByKey.Count;

    IEnumerable<ITable> IReadOnlyDictionary<string, ITable>.Values
    {
        get
        {
            foreach (var value in _tableByKey.Values)
            {
                yield return value;
            }
        }
    }

    ITable IReadOnlyDictionary<string, ITable>.this[string key] => _tableByKey[key];

    public TTable this[string name] => _tableByKey[name];

    public TTable GetOrAdd(string name)
    {
        lock (_lock)
        {
            return _tableByKey.GetOrAdd(name, (k, _) => Create(k), string.Empty);
        }
    }

    public bool ContainsKey(string name) => _tableByKey.ContainsKey(name);

    public bool TryGetValue(string name, [MaybeNullWhen(false)] out TTable value)
        => _tableByKey.TryGetValue(name, out value);

    public bool TryRemove(string name)
    {
        lock (_lock)
        {
            if (_tableByKey.TryRemove(name, out var value))
            {
                OnRemove(name, value);
                return true;
            }

            return false;
        }
    }

    public IEnumerator<KeyValuePair<string, TTable>> GetEnumerator() => _tableByKey.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _tableByKey.GetEnumerator();

    ITable IDatabase.GetOrAdd(string name) => GetOrAdd(name);

    bool IReadOnlyDictionary<string, ITable>.TryGetValue(string key, out ITable value)
    {
        if (_tableByKey.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }

        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<string, ITable>> IEnumerable<KeyValuePair<string, ITable>>.GetEnumerator()
    {
        foreach (var kvp in _tableByKey)
        {
            yield return new KeyValuePair<string, ITable>(kvp.Key, kvp.Value);
        }
    }

    protected abstract TTable Create(string key);

    protected virtual void OnRemove(string key, TTable value)
    {
    }
}
