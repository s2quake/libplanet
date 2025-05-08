using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Store.Trie;

public abstract class KeyValueStoreBase<TKey, TValue> : IDictionary<TKey, TValue>
    where TKey : notnull
    where TValue : notnull
{
    private readonly KeyCollection _keys;
    private readonly ValueCollection _values;

    protected KeyValueStoreBase()
    {
        _keys = new KeyCollection(this);
        _values = new ValueCollection(this);
    }

    public ICollection<TKey> Keys => _keys;

    ICollection<TValue> IDictionary<TKey, TValue>.Values => _values;

    public abstract int Count { get; }

    public bool IsReadOnly => false;

    public abstract TValue this[TKey key] { get; set; }

    public abstract bool Remove(TKey key);

    public abstract void Add(TKey key, TValue value);

    public abstract bool ContainsKey(TKey key);

    public abstract bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);

    public abstract void Clear();

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
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<TKey, TValue>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<TKey, TValue>(key, this[key]);
        }
    }

    protected abstract IEnumerable<TKey> EnumerateKeys();

    protected virtual bool CompareValue(TValue value1, TValue value2) => value1.Equals(value2);

    private sealed class KeyCollection(KeyValueStoreBase<TKey, TValue> owner) : ICollection<TKey>
    {
        public int Count => owner.Count;

        public bool IsReadOnly => true;

        public void Add(TKey item) => throw new NotSupportedException("Add is not supported.");

        public void Clear() => throw new NotSupportedException("Clear is not supported.");

        public bool Contains(TKey item) => owner.ContainsKey(item);

        public void CopyTo(TKey[] array, int arrayIndex)
            => throw new NotSupportedException("CopyTo is not supported.");

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

    private sealed class ValueCollection(KeyValueStoreBase<TKey, TValue> owner) : ICollection<TValue>
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

public abstract class KeyValueStoreBase : KeyValueStoreBase<KeyBytes, byte[]>
{
    protected override bool CompareValue(byte[] value1, byte[] value2) => value1.SequenceEqual(value2);
}
