using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public abstract class Database<TValue> : IDatabase
    where TValue : IDictionary<KeyBytes, byte[]>
{
    private readonly ConcurrentDictionary<string, TValue> _collectionByKey = new();

    public IEnumerable<string> Keys => _collectionByKey.Keys;

    public IEnumerable<TValue> Values => _collectionByKey.Values;

    public int Count => _collectionByKey.Count;

    IEnumerable<IDictionary<KeyBytes, byte[]>> IReadOnlyDictionary<string, IDictionary<KeyBytes, byte[]>>.Values
    {
        get
        {
            foreach (var value in _collectionByKey.Values)
            {
                yield return value;
            }
        }
    }

    IDictionary<KeyBytes, byte[]> IReadOnlyDictionary<string, IDictionary<KeyBytes, byte[]>>.this[string key]
        => _collectionByKey[key];

    public TValue this[string key] => _collectionByKey[key];

    public TValue GetOrAdd(string key) => _collectionByKey.GetOrAdd(key, Create);

    public bool ContainsKey(string key) => _collectionByKey.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
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

    public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
        => _collectionByKey.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _collectionByKey.GetEnumerator();

    protected abstract TValue Create(string key);

    protected virtual void OnRemove(string key, TValue value)
    {
    }

    IDictionary<KeyBytes, byte[]> IDatabase.GetOrAdd(string key) => GetOrAdd(key);

    bool IReadOnlyDictionary<string, IDictionary<KeyBytes, byte[]>>.TryGetValue(string key, out IDictionary<KeyBytes, byte[]> value)
    {
        if (_collectionByKey.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }

        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<string, IDictionary<KeyBytes, byte[]>>> IEnumerable<KeyValuePair<string, IDictionary<KeyBytes, byte[]>>>.GetEnumerator()
    {
        foreach (var kvp in _collectionByKey)
        {
            yield return new KeyValuePair<string, IDictionary<KeyBytes, byte[]>>(kvp.Key, kvp.Value);
        }
    }
}
