using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Data;

public sealed class MemoryTable(string name) : TableBase(name)
{
    private readonly ConcurrentDictionary<string, byte[]> _dictionary = new();

    public MemoryTable()
        : this(string.Empty)
    {
    }

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
            throw new ArgumentException($"Key '{key}' already exists in the table.", nameof(key));
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

