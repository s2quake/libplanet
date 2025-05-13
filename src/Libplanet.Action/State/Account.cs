using System.Diagnostics.CodeAnalysis;
using Libplanet.Serialization;
using Libplanet.Store.Trie;

namespace Libplanet.Action.State;

public sealed record class Account(ITrie Trie)
{
    public Account()
        : this(new Trie())
    {
    }

    public object GetValue(KeyBytes key)
    {
        var value = Trie[key];
        return ModelSerializer.Deserialize(value)
            ?? throw new InvalidOperationException("Failed to deserialize state.");
    }

    public Account SetValue(KeyBytes key, object value)
    {
        var k = key;
        var v = ModelSerializer.Serialize(value);
        var trie = Trie.Set(k, v);
        return new(trie);
    }

    public object? GetValueOrDefault(KeyBytes key) => TryGetValue(key, out object? state) ? state : null;

    public T GetValueOrFallback<T>(KeyBytes key, T fallback)
        => GetValueOrDefault(key) is T state ? state : fallback;

    public bool ContainsKey(KeyBytes key) => Trie.TryGetValue(key, out _);

    public Account RemoveValue(KeyBytes key) => new(Trie.Remove(key));

    public bool TryGetValue(KeyBytes key, [MaybeNullWhen(false)] out object value)
    {
        if (Trie.TryGetValue(key, out var v))
        {
            value = ModelSerializer.Deserialize(v)
                ?? throw new InvalidOperationException("Failed to deserialize state.");
            return true;
        }

        value = null;
        return false;
    }

    public bool TryGetValue<T>(KeyBytes key, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetValue(key, out var state) && state is T obj)
        {
            value = obj;
            return true;
        }

        value = default;
        return false;
    }
}
