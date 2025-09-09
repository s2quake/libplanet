using System.Collections;
using System.Diagnostics.CodeAnalysis;
using LruCacheNet;
using Libplanet.Types.Threading;

namespace Libplanet.Data;

public abstract class IndexBase<TKey, TValue>
    : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IRepositoryIndex
    where TKey : notnull
    where TValue : notnull
{
    private readonly LruCache<TKey, TValue>? _cache;
    private readonly ITable _table;
    private readonly ReaderWriterLockSlim _lock = new();

    private readonly KeyCollection _keys;
    private readonly ValueCollection _values;

    protected IndexBase(ITable table, int cacheSize = 100)
    {
        if (cacheSize > 0)
        {
            _cache = new LruCache<TKey, TValue>(cacheSize);
        }

        _table = table;
        _keys = new KeyCollection(this);
        _values = new ValueCollection(this);
    }

    public ICollection<TKey> Keys => _keys;

    public ICollection<TValue> Values => _values;

    public bool IsEmpty => _table.Count is 0;

    public int Count => _table.Count;

    public bool IsReadOnly => false;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _keys;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _values;

    string IRepositoryIndex.Name => _table.Name;

    ITable IRepositoryIndex.Table => _table;

    public TValue this[TKey key]
    {
        get
        {
            using var scope = new ReadScope(_lock);
            if (_cache?.TryGetValue(key, out var value) is true)
            {
                return value;
            }

            var bytes = _table[KeyToString(key)];
            value = BytesToValue(bytes);
            _cache?.AddOrUpdate(key, value);
            return value;
        }

        set
        {
            using var scope = new WriteScope(_lock);
            UpsertInternal(key, value);
        }
    }

    public bool Remove(TKey key)
    {
        using var scope = new WriteScope(_lock);
        return RemoveInternal(key);
    }

    public int RemoveRange(IEnumerable<TKey> keys)
    {
        using var scope = new WriteScope(_lock);
        var items = keys.ToArray();
        var count = 0;
        foreach (var item in items)
        {
            if (RemoveInternal(item))
            {
                count++;
            }
        }

        return count;
    }

    public bool TryAdd(TKey key, TValue value)
    {
        using var scope = new WriteScope(_lock);
        if (_cache?.TryGetValue(key, out _) is true)
        {
            return false;
        }

        if (_table.ContainsKey(KeyToString(key)))
        {
            return false;
        }

        _table.Add(KeyToString(key), ValueToBytes(value));
        _cache?.AddOrUpdate(key, value);
        OnUpdated(key, value);
        return true;
    }

    public void Add(TKey key, TValue value)
    {
        using var scope = new WriteScope(_lock);
        _table.Add(KeyToString(key), ValueToBytes(value));
        _cache?.AddOrUpdate(key, value);
        OnUpdated(key, value);
    }

    public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> keyValues)
    {
        using var scope = new WriteScope(_lock);
        var items = keyValues.ToArray();
        foreach (var item in items)
        {
            if (ContainsKeyInternal(item.Key))
            {
                throw new ArgumentException($"Key '{item.Key}' already exists in the index.", nameof(keyValues));
            }
        }

        foreach (var item in items)
        {
            var key = item.Key;
            var value = item.Value;
            _table.Add(KeyToString(key), ValueToBytes(value));
            _cache?.AddOrUpdate(key, value);
            OnUpdated(key, value);
        }
    }

    public void UpsertRange(IEnumerable<KeyValuePair<TKey, TValue>> keyValues)
    {
        using var scope = new WriteScope(_lock);
        var items = keyValues.ToArray();
        foreach (var item in items)
        {
            UpsertInternal(item.Key, item.Value);
        }
    }

    public bool ContainsKey(TKey key)
    {
        using var scope = new ReadScope(_lock);
        if (_cache?.ContainsKey(key) is true)
        {
            return true;
        }

        return _table.ContainsKey(KeyToString(key));
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        using var scope = new ReadScope(_lock);
        if (_cache?.TryGetValue(key, out value) is true && value is not null)
        {
            return true;
        }

        if (_table.TryGetValue(KeyToString(key), out var bytes))
        {
            value = BytesToValue(bytes);
            _cache?.AddOrUpdate(key, value);
            return true;
        }

        value = default;
        return false;
    }

    public void Clear()
    {
        using var scope = new WriteScope(_lock);
        _cache?.Clear();
        _table.Clear();
        OnCleared();
    }

    public void ClearCache()
    {
        using var scope = new WriteScope(_lock);
        _cache?.Clear();
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        => Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        => TryGetValue(item.Key, out var value) && CompareValue(value, item.Value);

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (arrayIndex < 0 || arrayIndex + Count > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        var index = arrayIndex;
        foreach (var key in EnumerateKeys())
        {
            array[index++] = new KeyValuePair<TKey, TValue>(key, this[key]);
        }
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        if (TryGetValue(item.Key, out var value) && CompareValue(value, item.Value))
        {
            return Remove(item.Key);
        }

        return false;
    }

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
    {
        foreach (var key in EnumerateKeys())
        {
            yield return new KeyValuePair<TKey, TValue>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var key in EnumerateKeys())
        {
            yield return new KeyValuePair<TKey, TValue>(key, this[key]);
        }
    }

    protected IEnumerable<TKey> EnumerateKeys()
    {
        foreach (var key in _table.Keys)
        {
            yield return StringToKey(key);
        }
    }

    protected virtual bool CompareValue(TValue value1, TValue value2) => value1.Equals(value2);

    protected abstract string KeyToString(TKey key);

    protected abstract TKey StringToKey(string key);

    protected abstract byte[] ValueToBytes(TValue value);

    protected abstract TValue BytesToValue(byte[] bytes);

    protected virtual void OnCleared()
    {
    }

    protected virtual void OnUpdated(TKey key, TValue item)
    {
    }

    protected virtual void OnRemoved(TKey key, TValue item)
    {
    }

    private bool ContainsKeyInternal(TKey key)
    {
        if (_cache?.TryGetValue(key, out _) is true)
        {
            return true;
        }

        return _table.ContainsKey(KeyToString(key));
    }

    private bool RemoveInternal(TKey key)
    {
        if (_cache?.Remove(key, out var value) is true)
        {
            _table.Remove(KeyToString(key));
            OnRemoved(key, value);
            return true;
        }
        else
        {
            var keyBytes = KeyToString(key);
            if (_table.TryGetValue(keyBytes, out var bytes))
            {
                value = BytesToValue(bytes);
                _table.Remove(keyBytes);
                OnRemoved(key, value);
                return true;
            }
        }

        return false;
    }

    private void UpsertInternal(TKey key, TValue value)
    {
        _table[KeyToString(key)] = ValueToBytes(value);
        _cache?.AddOrUpdate(key, value);
        OnUpdated(key, value);
    }

    private sealed class KeyCollection(IndexBase<TKey, TValue> owner) : ICollection<TKey>
    {
        public int Count => owner.Count;

        public bool IsReadOnly => true;

        public void Add(TKey item) => throw new NotSupportedException("Add is not supported.");

        public void Clear() => throw new NotSupportedException("Clear is not supported.");

        public bool Contains(TKey item) => owner.ContainsKey(item);

        public void CopyTo(TKey[] array, int arrayIndex)
        {
            if (arrayIndex < 0 || arrayIndex + Count > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            foreach (var key in owner.EnumerateKeys())
            {
                array[arrayIndex++] = key;
            }
        }

        public IEnumerator<TKey> GetEnumerator()
        {
            foreach (var key in owner.EnumerateKeys())
            {
                yield return key;
            }
        }

        public bool Remove(TKey item) => throw new NotSupportedException("Remove is not supported.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ValueCollection(IndexBase<TKey, TValue> owner) : ICollection<TValue>
    {
        public int Count => owner.Count;

        public bool IsReadOnly => true;

        public void Add(TValue item) => throw new NotSupportedException("Add is not supported.");

        public void Clear() => throw new NotSupportedException("Clear is not supported.");

        public bool Contains(TValue item) => throw new NotSupportedException("Contains is not supported.");

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            if (arrayIndex < 0 || arrayIndex + Count > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            foreach (var key in owner.EnumerateKeys())
            {
                array[arrayIndex++] = owner[key];
            }
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            foreach (var key in owner.EnumerateKeys())
            {
                yield return owner[key];
            }
        }

        public bool Remove(TValue item) => throw new NotSupportedException("Remove is not supported.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
