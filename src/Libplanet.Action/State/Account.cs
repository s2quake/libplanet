using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.DataStructures;

namespace Libplanet.Action.State;

public sealed record class Account(ITrie Trie)
{
    public Account()
        : this(new Trie())
    {
    }

    public object GetValue(string key) => Trie[key];

    public Account SetValue(string key, object value) => new(Trie.Set(key, value));

    public object? GetValueOrDefault(string key) => TryGetValue(key, out object? state) ? state : null;

    public T GetValueOrFallback<T>(string key, T fallback) => GetValueOrDefault(key) is T state ? state : fallback;

    public bool ContainsKey(string key) => Trie.TryGetValue(key, out _);

    public Account RemoveValue(string key) => new(Trie.Remove(key));

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value) => Trie.TryGetValue(key, out value);

    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
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
