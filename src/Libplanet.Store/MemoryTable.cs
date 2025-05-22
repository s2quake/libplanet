using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public sealed class MemoryTable : TableBase
{
    private readonly ConcurrentDictionary<string, byte[]> _dictionary = new();

    public override int Count => _dictionary.Count;

    public override byte[] this[string key]
    {
        get => _dictionary[key];
        set => _dictionary[key] = value;
    }

    public override void Add(string key, byte[] value)
    {
        if (!_dictionary.TryAdd(key, value))
        {
            throw new ArgumentException("An item with the same key has already been added.", nameof(key));
        }
    }

    public override void Clear() => _dictionary.Clear();

    public override bool ContainsKey(string key) => _dictionary.ContainsKey(key);

    public override bool Remove(string key) => _dictionary.TryRemove(key, out _);

    public override bool TryGetValue(string key, [MaybeNullWhen(false)] out byte[] value)
        => _dictionary.TryGetValue(key, out value);

    protected override IEnumerable<string> EnumerateKeys()
    {
        foreach (var item in _dictionary)
        {
            yield return item.Key;
        }
    }
}
