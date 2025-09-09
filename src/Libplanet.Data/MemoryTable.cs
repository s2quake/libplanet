using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Data;

public sealed class MemoryTable(string name) : TableBase(name)
{
    private readonly Dictionary<string, byte[]> _dictionary = new();

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

    protected override bool RemoveOverride(string key) => _dictionary.Remove(key);

    protected override void ClearOverride() => _dictionary.Clear();

    protected override IEnumerable<(string, byte[]?)> EnumerateOverride(bool includeValue)
    {
        foreach (var (key, value) in _dictionary)
        {
            yield return (key, includeValue ? value : null);
        }
    }
}

