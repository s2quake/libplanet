using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Store.Trie;

public abstract class KeyValueStoreBase : IDictionary<KeyBytes, byte[]>
{
    private readonly KeyCollection _keys;
    private readonly ValueCollection _values;

    protected KeyValueStoreBase()
    {
        _keys = new KeyCollection(this);
        _values = new ValueCollection(this);
    }

    public ICollection<KeyBytes> Keys => _keys;

    ICollection<byte[]> IDictionary<KeyBytes, byte[]>.Values => _values;

    int ICollection<KeyValuePair<KeyBytes, byte[]>>.Count => throw new NotSupportedException();

    public bool IsReadOnly => false;

    public abstract byte[] this[KeyBytes key] { get; set; }

    public abstract bool Remove(KeyBytes key);

    public abstract void Add(KeyBytes key, byte[] value);

    public abstract bool ContainsKey(KeyBytes key);

    public abstract bool TryGetValue(KeyBytes key, [MaybeNullWhen(false)] out byte[] value);

    public abstract void Clear();

    void ICollection<KeyValuePair<KeyBytes, byte[]>>.Add(KeyValuePair<KeyBytes, byte[]> item)
        => Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<KeyBytes, byte[]>>.Contains(KeyValuePair<KeyBytes, byte[]> item)
        => TryGetValue(item.Key, out var value) && value.SequenceEqual(item.Value);

    void ICollection<KeyValuePair<KeyBytes, byte[]>>.CopyTo(KeyValuePair<KeyBytes, byte[]>[] array, int arrayIndex)
        => throw new NotSupportedException("CopyTo is not supported.");

    bool ICollection<KeyValuePair<KeyBytes, byte[]>>.Remove(KeyValuePair<KeyBytes, byte[]> item)
    {
        if (TryGetValue(item.Key, out var value) && value.SequenceEqual(item.Value))
        {
            return Remove(item.Key);
        }

        return false;
    }

    IEnumerator<KeyValuePair<KeyBytes, byte[]>> IEnumerable<KeyValuePair<KeyBytes, byte[]>>.GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<KeyBytes, byte[]>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<KeyBytes, byte[]>(key, this[key]);
        }
    }

    protected abstract IEnumerable<KeyBytes> EnumerateKeys();

    private sealed class KeyCollection(KeyValueStoreBase owner) : ICollection<KeyBytes>
    {
        public int Count => throw new NotSupportedException("Count is not supported.");

        public bool IsReadOnly => true;

        public void Add(KeyBytes item) => throw new NotSupportedException("Add is not supported.");

        public void Clear() => throw new NotSupportedException("Clear is not supported.");

        public bool Contains(KeyBytes item) => owner.ContainsKey(item);

        public void CopyTo(KeyBytes[] array, int arrayIndex)
            => throw new NotSupportedException("CopyTo is not supported.");

        public IEnumerator<KeyBytes> GetEnumerator()
        {
            foreach (var key in owner.EnumerateKeys())
            {
                yield return key;
            }
        }

        public bool Remove(KeyBytes item) => throw new NotSupportedException("Remove is not supported.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ValueCollection(KeyValueStoreBase owner) : ICollection<byte[]>
    {
        public int Count => throw new NotSupportedException("Count is not supported.");

        public bool IsReadOnly => true;

        public void Add(byte[] item) => throw new NotSupportedException("Add is not supported.");

        public void Clear() => throw new NotSupportedException("Clear is not supported.");

        public bool Contains(byte[] item) => throw new NotSupportedException("Contains is not supported.");

        public void CopyTo(byte[][] array, int arrayIndex)
            => throw new NotSupportedException("CopyTo is not supported.");

        public IEnumerator<byte[]> GetEnumerator()
        {
            foreach (var key in owner.EnumerateKeys())
            {
                yield return owner[key];
            }
        }

        public bool Remove(byte[] item) => throw new NotSupportedException("Remove is not supported.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
