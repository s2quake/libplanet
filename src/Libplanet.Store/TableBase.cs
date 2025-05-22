using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Store;

public abstract class TableBase : ITable
{
    private readonly KeyCollection _keys;
    private readonly ValueCollection _values;

    protected TableBase()
    {
        _keys = new KeyCollection(this);
        _values = new ValueCollection(this);
    }

    public ICollection<string> Keys => _keys;

    ICollection<byte[]> IDictionary<string, byte[]>.Values => _values;

    public abstract int Count { get; }

    public bool IsReadOnly => false;

    public abstract byte[] this[string key] { get; set; }

    public abstract bool Remove(string key);

    public abstract void Add(string key, byte[] value);

    public abstract bool ContainsKey(string key);

    public abstract bool TryGetValue(string key, [MaybeNullWhen(false)] out byte[] value);

    public abstract void Clear();

    void ICollection<KeyValuePair<string, byte[]>>.Add(KeyValuePair<string, byte[]> item)
        => Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<string, byte[]>>.Contains(KeyValuePair<string, byte[]> item)
        => TryGetValue(item.Key, out var value) && CompareValue(value, item.Value);

    void ICollection<KeyValuePair<string, byte[]>>.CopyTo(KeyValuePair<string, byte[]>[] array, int arrayIndex)
        => throw new NotSupportedException("CopyTo is not supported.");

    bool ICollection<KeyValuePair<string, byte[]>>.Remove(KeyValuePair<string, byte[]> item)
    {
        if (TryGetValue(item.Key, out var value) && CompareValue(value, item.Value))
        {
            return Remove(item.Key);
        }

        return false;
    }

    IEnumerator<KeyValuePair<string, byte[]>> IEnumerable<KeyValuePair<string, byte[]>>.GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<string, byte[]>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<string, byte[]>(key, this[key]);
        }
    }

    protected abstract IEnumerable<string> EnumerateKeys();

    protected virtual bool CompareValue(byte[] value1, byte[] value2) => value1.SequenceEqual(value2);

    private sealed class KeyCollection(TableBase owner) : ICollection<string>
    {
        public int Count => owner.Count;

        public bool IsReadOnly => true;

        public void Add(string item) => throw new NotSupportedException("Add is not supported.");

        public void Clear() => throw new NotSupportedException("Clear is not supported.");

        public bool Contains(string item) => owner.ContainsKey(item);

        public void CopyTo(string[] array, int arrayIndex)
            => throw new NotSupportedException("CopyTo is not supported.");

        public IEnumerator<string> GetEnumerator()
        {
            foreach (var key in owner.EnumerateKeys())
            {
                yield return key;
            }
        }

        public bool Remove(string item) => throw new NotSupportedException("Remove is not supported.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ValueCollection(TableBase owner) : ICollection<byte[]>
    {
        public int Count => owner.Count;

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
