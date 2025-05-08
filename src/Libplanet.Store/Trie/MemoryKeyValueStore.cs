using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Store.Trie;

public sealed class MemoryKeyValueStore : IDictionary<KeyBytes, byte[]>
{
    private readonly ConcurrentDictionary<KeyBytes, byte[]> _dictionary = new();

    public ICollection<KeyBytes> Keys => _dictionary.Keys;

    public ICollection<byte[]> Values => _dictionary.Values;

    public int Count => _dictionary.Count;

    public bool IsReadOnly => false;

    public byte[] this[KeyBytes key]
    {
        get => _dictionary[key];
        set => _dictionary[key] = value;
    }

    public void Add(KeyBytes key, byte[] value)
    {
        if (!_dictionary.TryAdd(key, value))
        {
            throw new ArgumentException("An item with the same key has already been added.", nameof(key));
        }
    }

    public void Add(KeyValuePair<KeyBytes, byte[]> item) => Add(item.Key, item.Value);

    public void Clear() => _dictionary.Clear();

    public bool Contains(KeyValuePair<KeyBytes, byte[]> item)
        => _dictionary.TryGetValue(item.Key, out var value) && value.SequenceEqual(item.Value);

    public bool ContainsKey(KeyBytes key) => _dictionary.ContainsKey(key);

    public void CopyTo(KeyValuePair<KeyBytes, byte[]>[] array, int arrayIndex)
    {
        if (arrayIndex < 0 || arrayIndex > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("The number of elements in the source is greater than the available space in the array.");
        }

        var i = arrayIndex;
        foreach (var item in _dictionary)
        {
            array[i++] = item;
        }
    }

    public IEnumerator<KeyValuePair<KeyBytes, byte[]>> GetEnumerator() => _dictionary.GetEnumerator();

    public bool Remove(KeyBytes key) => _dictionary.TryRemove(key, out _);

    public bool Remove(KeyValuePair<KeyBytes, byte[]> item) => Contains(item) && _dictionary.TryRemove(item.Key, out _);

    public bool TryGetValue(KeyBytes key, [MaybeNullWhen(false)] out byte[] value)
        => _dictionary.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
