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

    public override bool ContainsKey(string key) => _dictionary.ContainsKey(key);

    public override bool TryGetValue(string key, [MaybeNullWhen(false)] out byte[] value)
        => _dictionary.TryGetValue(key, out value);

    protected override byte[] GetOverride(string key) => _dictionary[key];

    protected override void SetOverride(string key, byte[] value) => _dictionary[key] = value;

    protected override void AddOverride(string key, byte[] value)
    {
        if (!_dictionary.TryAdd(key, value))
        {
            throw new ArgumentException($"Key '{key}' already exists in the table.", nameof(key));
        }
    }

    protected override bool RemoveOverride(string key) => _dictionary.TryRemove(key, out _);

    protected override void ClearOverride() => _dictionary.Clear();

    protected override IEnumerable<string> EnumerateKeys()
    {
        foreach (var item in _dictionary)
        {
            yield return item.Key;
        }
    }
}

