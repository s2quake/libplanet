using System.Collections;
using System.Diagnostics.CodeAnalysis;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Data;

public abstract class KeyedIndexBase<TKey, TValue>
    : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
    where TValue : IHasKey<TKey>
{
    private readonly ICache<TKey, TValue>? _cache;
    private readonly ITable _table;
    private readonly ReaderWriterLockSlim _lock = new();

    private readonly KeyCollection _keys;
    private readonly ValueCollection _values;

    protected KeyedIndexBase(ITable table, int cacheSize = 100)
    {
        if (cacheSize > 0)
        {
            _cache = new ConcurrentLruBuilder<TKey, TValue>()
                .WithCapacity(cacheSize)
                .Build();
        }

        _table = table;
        _table.Cleared += (s, e) => _cache?.Clear();
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

    public TValue this[TKey key]
    {
        get
        {
            using var scope = new ReadScope(_lock);
            if (_cache?.TryGet(key, out var value) is true)
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
            if (!Equals(key, value.Key))
            {
                throw new ArgumentException("Key and Value must match.", nameof(value));
            }

            UpsertInternal(value);
        }
    }

    public bool Remove(TKey key)
    {
        using var scope = new WriteScope(_lock);
        return RemoveInternal(key);
    }

    public bool Remove(TValue value)
    {
        using var scope = new WriteScope(_lock);
        return RemoveInternal(value.Key);
    }

    public int RemoveRange(IEnumerable<TKey> keys)
    {
        using var scope = new WriteScope(_lock);
        var items = keys.ToArray();
        int count = 0;

        foreach (var item in items)
        {
            if (RemoveInternal(item))
            {
                count++;
            }
        }

        return count;
    }

    public int RemoveRange(IEnumerable<TValue> values)
    {
        using var scope = new WriteScope(_lock);
        var items = values.ToArray();
        var count = 0;

        foreach (var item in items)
        {
            if (RemoveInternal(item.Key))
            {
                count++;
            }
        }

        return count;
    }

    public bool TryAdd(TValue value)
    {
        using var scope = new WriteScope(_lock);
        var key = value.Key;
        if (_cache?.TryGet(key, out _) is true)
        {
            return false;
        }

        if (_table.ContainsKey(KeyToString(key)))
        {
            return false;
        }

        _table.Add(KeyToString(key), ValueToBytes(value));
        _cache?.AddOrUpdate(key, value);
        OnUpdated(value);
        return true;
    }

    public void Add(TValue value)
    {
        using var scope = new WriteScope(_lock);
        var key = value.Key;
        _table.Add(KeyToString(key), ValueToBytes(value));
        _cache?.AddOrUpdate(key, value);
        OnUpdated(value);
    }

    public void AddRange(IEnumerable<TValue> values)
    {
        using var scope = new WriteScope(_lock);
        var items = values.ToArray();

        foreach (var item in items)
        {
            if (ContainsKeyInternal(item.Key))
            {
                throw new ArgumentException($"Key '{item.Key}' already exists in the index.", nameof(values));
            }
        }

        foreach (var item in items)
        {
            var key = item.Key;
            var value = item;
            _table.Add(KeyToString(key), ValueToBytes(value));
            _cache?.AddOrUpdate(key, value);
            OnUpdated(value);
        }
    }

    public void UpsertRange(IEnumerable<TValue> values)
    {
        using var scope = new WriteScope(_lock);
        var items = values.ToArray();
        foreach (var item in items)
        {
            UpsertInternal(item);
        }
    }

    public bool ContainsKey(TKey key)
    {
        using var scope = new ReadScope(_lock);
        if (_cache?.TryGet(key, out _) is true)
        {
            return true;
        }

        return _table.ContainsKey(KeyToString(key));
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        using var scope = new ReadScope(_lock);
        if (_cache?.TryGet(key, out value) is true && value is not null)
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


    void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
    {
        if (!Equals(key, value.Key))
        {
            throw new ArgumentException("Key and Value must match.", nameof(value));
        }

        Add(value);
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
    {
        if (!Equals(item.Key, item.Value.Key))
        {
            throw new ArgumentException("Key and Value must match.", nameof(item));
        }

        Add(item.Value);
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
    {
        return TryGetValue(item.Key, out var value) && Equals(item.Key, value.Key) && CompareValue(value, item.Value);
    }

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
        if (Equals(item.Key, item.Value.Key) && TryGetValue(item.Key, out var value) && CompareValue(value, item.Value))
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

    protected virtual bool CompareValue(TValue value1, TValue value2) => Equals(value1, value2);

    protected abstract string KeyToString(TKey key);

    protected abstract TKey StringToKey(string key);

    protected abstract byte[] ValueToBytes(TValue value);

    protected abstract TValue BytesToValue(byte[] bytes);

    protected virtual void OnCleared()
    {
    }

    protected virtual void OnUpdated(TValue item)
    {
    }

    protected virtual void OnRemoved(TValue item)
    {
    }

    private bool ContainsKeyInternal(TKey key)
    {
        if (_cache?.TryGet(key, out _) is true)
        {
            return true;
        }

        return _table.ContainsKey(KeyToString(key));
    }

    private bool RemoveInternal(TKey key)
    {
        if (_cache?.TryRemove(key, out var value) is true)
        {
            _table.Remove(KeyToString(key));
            OnRemoved(value);
            return true;
        }
        else
        {
            var keyBytes = KeyToString(key);
            if (_table.TryGetValue(keyBytes, out var bytes))
            {
                value = BytesToValue(bytes);
                _table.Remove(keyBytes);
                OnRemoved(value);
                return true;
            }
        }

        return false;
    }

    private void UpsertInternal(TValue value)
    {
        var key = value.Key;
        _table[KeyToString(key)] = ValueToBytes(value);
        _cache?.AddOrUpdate(key, value);
        OnUpdated(value);
    }

    private sealed class KeyCollection(KeyedIndexBase<TKey, TValue> owner) : ICollection<TKey>
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

    private sealed class ValueCollection(KeyedIndexBase<TKey, TValue> owner) : ICollection<TValue>
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
