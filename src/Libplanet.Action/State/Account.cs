using System.Diagnostics.CodeAnalysis;
using Libplanet.Types.Crypto;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public sealed record class Account(ITrie Trie)
{
    public Account()
        : this(new Trie())
    {
    }

    public object GetValue(Address address)
    {
        var value = Trie[ToStateKey(address)];
        return ModelSerializer.Deserialize(value)
            ?? throw new InvalidOperationException("Failed to deserialize state.");
    }

    public Account SetValue(Address address, object value)
    {
        var k = ToStateKey(address);
        var v = ModelSerializer.Serialize(value);
        var trie = Trie.Set(k, v);
        return new(trie);
    }

    public object? GetValueOrDefault(Address address) => TryGetValue(address, out object? state) ? state : null;

    public T GetValueOrFallback<T>(Address address, T fallback)
        => GetValueOrDefault(address) is T state ? state : fallback;

    public Account RemoveValue(Address address) => new(Trie.Remove(ToStateKey(address)));

    public bool TryGetValue(Address address, [MaybeNullWhen(false)] out object value)
    {
        var key = ToStateKey(address);
        if (Trie.TryGetValue(key, out var v))
        {
            value = ModelSerializer.Deserialize(v)
                ?? throw new InvalidOperationException("Failed to deserialize state.");
            return true;
        }

        value = null;
        return false;
    }

    public bool TryGetValue<T>(Address address, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetValue(address, out var state) && state is T obj)
        {
            value = obj;
            return true;
        }

        value = default;
        return false;
    }
}
