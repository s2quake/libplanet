using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public abstract class Database<TValue> : IReadOnlyDictionary<string, TValue>
    where TValue : IDictionary<KeyBytes, byte[]>
{
    private readonly ConcurrentDictionary<string, TValue> _collectionByKey = new();

    public IEnumerable<string> Keys => _collectionByKey.Keys;

    public IEnumerable<TValue> Values => _collectionByKey.Values;

    public int Count => _collectionByKey.Count;

    public TValue this[string key] => _collectionByKey[key];

    public TValue GetOrAdd(string key) => _collectionByKey.GetOrAdd(key, Create);

    public bool ContainsKey(string key) => _collectionByKey.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
        => _collectionByKey.TryGetValue(key, out value);

    IEnumerator<KeyValuePair<string, TValue>> IEnumerable<KeyValuePair<string, TValue>>.GetEnumerator()
        => _collectionByKey.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _collectionByKey.GetEnumerator();

    protected abstract TValue Create(string key);
}
