using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.Trie;
using LruCacheNet;

namespace Libplanet.Store;

public abstract class CollectionBase<TKey, TValue>
    : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IDisposable
    where TKey : notnull
    where TValue : notnull
{
    private readonly LruCache<TKey, TValue> _cache;
    private readonly IDictionary<KeyBytes, byte[]> _dictionary;

    private readonly KeyCollection _keys;
    private readonly ValueCollection _values;

    protected CollectionBase(IDictionary<KeyBytes, byte[]> dictionary, int cacheSize = 100)
    {
        _cache = new(cacheSize);
        _dictionary = dictionary;
        _keys = new KeyCollection(this);
        _values = new ValueCollection(this);
    }

    public ICollection<TKey> Keys => _keys;

    public ICollection<TValue> Values => _values;

    public int Count => _dictionary.Count;

    public bool IsReadOnly => false;

    protected bool IsDisposed { get; private set; }

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _keys;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _values;

    public TValue this[TKey key]
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (_cache.TryGetValue(key, out var value))
            {
                return value;
            }

            if (_dictionary[GetKeyBytes(key)] is { } bytes)
            {
                value = GetValue(bytes);
                _cache[key] = value;
                return value;
            }

            throw new KeyNotFoundException($"No such key: ${key}.");
        }

        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            OnSet(key, value);
            _dictionary[GetKeyBytes(key)] = GetBytes(value);
            _cache[key] = value;
            OnSetComplete(key, value);
        }
    }

    public bool Remove(TKey key)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        OnRemove(key);
        if (_cache.Remove(key, out var value))
        {
            _dictionary.Remove(GetKeyBytes(key));
            OnRemoveComplete(key, value);
            return true;
        }
        else if (_dictionary.TryGetValue(GetKeyBytes(key), out var bytes))
        {
            value = GetValue(bytes);
            OnRemoveComplete(key, value);
            return true;
        }

        return false;
    }

    public void Add(TKey key, TValue value)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        OnAdd(key, value);
        _dictionary.Add(GetKeyBytes(key), GetBytes(value));
        _cache[key] = value;
        OnAddComplete(key, value);
    }

    public bool ContainsKey(TKey key)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_cache.TryGetValue(key, out _))
        {
            return true;
        }

        return _dictionary.ContainsKey(GetKeyBytes(key));
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_cache.TryGetValue(key, out value) && value is not null)
        {
            return true;
        }

        if (_dictionary.TryGetValue(GetKeyBytes(key), out var bytes))
        {
            value = GetValue(bytes);
            _cache[key] = value;
            return true;
        }

        value = default;
        return false;
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        OnClear();
        _cache.Clear();
        _dictionary.Clear();
        OnClearComplete();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        => Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        => TryGetValue(item.Key, out var value) && CompareValue(value, item.Value);

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        => throw new NotSupportedException("CopyTo is not supported.");

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
        foreach (var key in _dictionary.Keys)
        {
            yield return GetKey(key);
        }
    }

    protected virtual bool CompareValue(TValue value1, TValue value2) => value1.Equals(value2);

    protected abstract KeyBytes GetKeyBytes(TKey key);

    protected abstract TKey GetKey(KeyBytes keyBytes);

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

    private sealed class KeyCollection(CollectionBase<TKey, TValue> owner) : ICollection<TKey>
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

    private sealed class ValueCollection(CollectionBase<TKey, TValue> owner) : ICollection<TValue>
    {
        public int Count => owner.Count;

        public bool IsReadOnly => true;

        public void Add(TValue item) => throw new NotSupportedException("Add is not supported.");

        public void Clear() => throw new NotSupportedException("Clear is not supported.");

        public bool Contains(TValue item) => throw new NotSupportedException("Contains is not supported.");

        public void CopyTo(TValue[] array, int arrayIndex)
            => throw new NotSupportedException("CopyTo is not supported.");

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
