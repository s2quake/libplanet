using System.Diagnostics.CodeAnalysis;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Crypto;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public sealed record class Account(ITrie Trie)
{
    public Account()
        : this(new Trie())
    {
    }

    public object GetValue(string key) => GetValue(ToStateKey(key));

    public object GetValue(Address key) => GetValue(ToStateKey(key));

    public object GetValue(KeyBytes key)
    {
        var value = Trie[key];
        return ModelSerializer.Deserialize(value)
            ?? throw new InvalidOperationException("Failed to deserialize state.");
    }

    public Account SetValue(string key, object value) => SetValue(ToStateKey(key), value);

    public Account SetValue(Address key, object value) => SetValue(ToStateKey(key), value);

    public Account SetValue(KeyBytes key, object value)
    {
        var k = key;
        var v = ModelSerializer.Serialize(value);
        var trie = Trie.Set(k, v);
        return new(trie);
    }

    public object? GetValueOrDefault(string key) => GetValueOrDefault(ToStateKey(key));

    public object? GetValueOrDefault(Address key) => GetValueOrDefault(ToStateKey(key));

    public object? GetValueOrDefault(KeyBytes key) => TryGetValue(key, out object? state) ? state : null;

    public T GetValueOrFallback<T>(string key, T fallback) => GetValueOrFallback(ToStateKey(key), fallback);

    public T GetValueOrFallback<T>(Address key, T fallback) => GetValueOrFallback(ToStateKey(key), fallback);

    public T GetValueOrFallback<T>(KeyBytes key, T fallback)
        => GetValueOrDefault(key) is T state ? state : fallback;

    public Account RemoveValue(string key) => RemoveValue(ToStateKey(key));

    public Account RemoveValue(Address key) => RemoveValue(ToStateKey(key));

    public Account RemoveValue(KeyBytes key) => new(Trie.Remove(key));

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
        => TryGetValue(ToStateKey(key), out value);

    public bool TryGetValue(Address key, [MaybeNullWhen(false)] out object value)
        => TryGetValue(ToStateKey(key), out value);

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

    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
        => TryGetValue(ToStateKey(key), out value);

    public bool TryGetValue<T>(Address key, [MaybeNullWhen(false)] out T value)
        => TryGetValue(ToStateKey(key), out value);

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
