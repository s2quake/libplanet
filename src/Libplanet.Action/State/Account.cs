using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.Trie;

namespace Libplanet.Action.State;

public sealed record class Account(ITrie Trie)
{
    public Account()
        : this(new Trie())
    {
    }

    public object GetValue(KeyBytes key) => Trie[key];

    public Account SetValue(KeyBytes key, object value) => new(Trie.Set(key, value));

    public object? GetValueOrDefault(KeyBytes key) => TryGetValue(key, out object? state) ? state : null;

    public T GetValueOrFallback<T>(KeyBytes key, T fallback) => GetValueOrDefault(key) is T state ? state : fallback;

    public bool ContainsKey(KeyBytes key) => Trie.TryGetValue(key, out _);

    public Account RemoveValue(KeyBytes key) => new(Trie.Remove(key));

    public bool TryGetValue(KeyBytes key, [MaybeNullWhen(false)] out object value) => Trie.TryGetValue(key, out value);

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
