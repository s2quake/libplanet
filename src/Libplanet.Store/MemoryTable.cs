using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public sealed class MemoryTable : TableBase
{
    private readonly ConcurrentDictionary<KeyBytes, byte[]> _dictionary = new();

    public override int Count => _dictionary.Count;

    public override byte[] this[KeyBytes key]
    {
        get => _dictionary[key];
        set => _dictionary[key] = value;
    }

    public override void Add(KeyBytes key, byte[] value)
    {
        if (!_dictionary.TryAdd(key, value))
        {
            throw new ArgumentException("An item with the same key has already been added.", nameof(key));
        }
    }

    public override void Clear() => _dictionary.Clear();

    public override bool ContainsKey(KeyBytes key) => _dictionary.ContainsKey(key);

    public override bool Remove(KeyBytes key) => _dictionary.TryRemove(key, out _);

    public override bool TryGetValue(KeyBytes key, [MaybeNullWhen(false)] out byte[] value)
        => _dictionary.TryGetValue(key, out value);

    protected override IEnumerable<KeyBytes> EnumerateKeys()
    {
        foreach (var item in _dictionary)
        {
            yield return item.Key;
        }
    }
}
