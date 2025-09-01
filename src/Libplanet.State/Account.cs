using System.Diagnostics.CodeAnalysis;
using Libplanet.State.Structures;

namespace Libplanet.State;

public sealed record class Account(Trie Trie)
{
    public Account()
        : this(new Trie())
    {
    }

    public object GetValue(string key) => Trie[key];

    public Account SetValue(string key, object value) => new(Trie.Set(key, value));

    public object? GetValueOrDefault(string key) => TryGetValue(key, out object? state) ? state : null;

    public T GetValueOrDefault<T>(string key, [MaybeNullWhen(false)] T defaultValue)
        where T : notnull
    {
        if (GetValueOrDefault(key) is { } v)
        {
            if (v is T t)
            {
                return t;
            }

            var message = $"The value stored at the key '{key}' cannot be cast to the type '{typeof(T)}'. " +
                          $"stored type: '{v.GetType()}'.";
            throw new InvalidCastException(message);
        }

        return defaultValue;
    }

    public bool ContainsKey(string key) => Trie.TryGetValue(key, out _);

    public Account RemoveValue(string key) => new(Trie.Remove(key));

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value) => Trie.TryGetValue(key, out value);

    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
        where T : notnull
    {
        if (TryGetValue(key, out var state))
        {
            if (state is T t)
            {
                value = t;
                return true;
            }
            var message = $"The value stored at the key '{key}' cannot be cast to the type '{typeof(T)}'. " +
                          $"stored type: '{state.GetType()}'.";
            throw new InvalidCastException(message);
        }

        value = default;
        return false;
    }
}
