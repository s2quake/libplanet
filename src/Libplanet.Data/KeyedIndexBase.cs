using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Data;

public abstract class KeyedIndexBase<TKey, TValue>
    : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IDisposable
    where TKey : notnull
    where TValue : IHasKey<TKey>
{
    private readonly ICache<TKey, TValue> _cache;
    private readonly ITable _table;
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly TypeConverter _keyConverter = TypeDescriptor.GetConverter(typeof(TKey));

    private readonly KeyCollection _keys;
    private readonly ValueCollection _values;

    protected KeyedIndexBase(ITable dictionary, int cacheSize = 100)
    {
        _cache = new ConcurrentLruBuilder<TKey, TValue>()
            .WithCapacity(cacheSize)
            .Build();
        _table = dictionary;
        _keys = new KeyCollection(this);
        _values = new ValueCollection(this);
    }

    public ICollection<TKey> Keys => _keys;

    public ICollection<TValue> Values => _values;

    public int Count => _table.Count;

    public bool IsReadOnly => false;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _keys;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _values;

    protected bool IsDisposed { get; private set; }

    public TValue this[TKey key]
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            using var scope = new ReadScope(_lock);
            if (_cache.TryGet(key, out var value))
            {
                return value;
            }

            var bytes = _table[GetKeyBytes(key)];
            value = GetValue(bytes);
            _cache.AddOrUpdate(key, value);
            return value;
        }

        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            using var scope = new WriteScope(_lock);

            if (!Equals(key, value.Key))
            {
                throw new ArgumentException("Key and Value must match.", nameof(value));
            }

            OnSet(key, value);
            _table[GetKeyBytes(key)] = GetBytes(value);
            _cache.AddOrUpdate(key, value);
            OnSetComplete(key, value);
        }
    }

    public bool Remove(TKey key)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        using var scope = new WriteScope(_lock);
        return RemoveInternal(key);
    }

    public bool Remove(TValue value)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        using var scope = new WriteScope(_lock);
        return RemoveInternal(value.Key);
    }

    public void RemoveRange(IEnumerable<TKey> keys)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        using var scope = new WriteScope(_lock);
        var items = keys.Where(ContainsKeyInternal).ToArray();
        foreach (var item in items)
        {
            RemoveInternal(item);
        }
    }

    public void RemoveRange(IEnumerable<TValue> values)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        using var scope = new WriteScope(_lock);
        var items = values.Where(item => ContainsKeyInternal(item.Key)).ToArray();
        foreach (var item in items)
        {
            RemoveInternal(item.Key);
        }
    }

    public bool TryAdd(TValue value)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        using var scope = new WriteScope(_lock);
        var key = value.Key;
        if (_cache.TryGet(key, out _))
        {
            return false;
        }

        if (_table.ContainsKey(GetKeyBytes(key)))
        {
            return false;
        }

        OnAdd(key, value);
        _table.Add(GetKeyBytes(key), GetBytes(value));
        _cache.AddOrUpdate(key, value);
        OnAddComplete(key, value);
        return true;
    }

    public void Add(TValue value)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        using var scope = new WriteScope(_lock);
        var key = value.Key;
        OnAdd(key, value);
        _table.Add(GetKeyBytes(key), GetBytes(value));
        _cache.AddOrUpdate(key, value);
        OnAddComplete(key, value);
    }

    public void AddRange(IEnumerable<TValue> values)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        using var scope = new WriteScope(_lock);
        foreach (var value in values)
        {
            var key = value.Key;
            OnAdd(key, value);
            _table.Add(GetKeyBytes(key), GetBytes(value));
            _cache.AddOrUpdate(key, value);
            OnAddComplete(key, value);
        }
    }

    public bool ContainsKey(TKey key)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        using var scope = new ReadScope(_lock);
        if (_cache.TryGet(key, out _))
        {
            return true;
        }

        return _table.ContainsKey(GetKeyBytes(key));
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        using var scope = new ReadScope(_lock);
        if (_cache.TryGet(key, out value) && value is not null)
        {
            return true;
        }

        if (_table.TryGetValue(GetKeyBytes(key), out var bytes))
        {
            value = GetValue(bytes);
            _cache.AddOrUpdate(key, value);
            return true;
        }

        value = default;
        return false;
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        using var scope = new WriteScope(_lock);
        OnClear();
        _cache.Clear();
        _table.Clear();
        OnClearComplete();
    }

    public void Clear(Func<TValue, bool> validator)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        using var scope = new WriteScope(_lock);

        foreach (var (key, value) in this.ToArray())
        {
            if (!validator(value))
            {
                RemoveInternal(key);
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
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
        => throw new NotSupportedException("CopyTo is not supported.");

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
            yield return GetKey(key);
        }
    }

    protected virtual bool CompareValue(TValue value1, TValue value2) => value1.Equals(value2);

    protected string GetKeyBytes(TKey key)
    {
        if (_keyConverter.ConvertToInvariantString(key) is not string s)
        {
            throw new InvalidOperationException($"Cannot convert {key} to string.");
        }

        return s;
    }

    protected TKey GetKey(string s)
    {
        if (_keyConverter.ConvertFromInvariantString(s) is not TKey key)
        {
            throw new InvalidOperationException($"Cannot convert {s} to {typeof(TKey)}.");
        }

        return key;
    }

    protected abstract byte[] GetBytes(TValue value);

    protected abstract TValue GetValue(byte[] bytes);

    protected virtual void OnClear()
    {
    }

    protected virtual void OnClearComplete()
    {
    }

    protected virtual void OnAdd(TKey key, TValue item)
    {
    }

    protected virtual void OnAddComplete(TKey key, TValue item)
    {
    }

    protected virtual void OnRemove(TKey key)
    {
    }

    protected virtual void OnRemoveComplete(TKey key, TValue item)
    {
    }

    protected virtual void OnSet(TKey key, TValue item)
    {
    }

    protected virtual void OnSetComplete(TKey key, TValue item)
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
        }
    }

    private bool ContainsKeyInternal(TKey key)
    {
        if (_cache.TryGet(key, out _))
        {
            return true;
        }

        return _table.ContainsKey(GetKeyBytes(key));
    }

    private bool RemoveInternal(TKey key)
    {
        OnRemove(key);
        if (_cache.TryRemove(key, out var value))
        {
            _table.Remove(GetKeyBytes(key));
            OnRemoveComplete(key, value);
            return true;
        }
        else if (_table.TryGetValue(GetKeyBytes(key), out var bytes))
        {
            value = GetValue(bytes);
            OnRemoveComplete(key, value);
            return true;
        }

        return false;
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
            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            if (Count > array.Length + arrayIndex)
            {
                var message = "The number of elements in the source KeyCollection is greater than the " +
                              "available space from arrayIndex to the end of the destination array.";
                throw new ArgumentException(message, nameof(array));
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
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            if (Count > array.Length + arrayIndex)
            {
                var message = "The number of elements in the source ValueCollection is greater than the " +
                              "available space from arrayIndex to the end of the destination array.";
                throw new ArgumentException(message, nameof(array));
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
