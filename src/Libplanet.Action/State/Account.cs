using System.Diagnostics.CodeAnalysis;
using Libplanet.Crypto;
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

    public object GetState(Address address)
    {
        var value = Trie[ToStateKey(address)];
        // if (ModelSerializer.TryGetType(value, out var type))
        // {
        //     return ModelSerializer.Deserialize(value, type)
        //         ?? throw new InvalidOperationException("Failed to deserialize state.");
        // }

        return ModelSerializer.Deserialize(value)
            ?? throw new InvalidOperationException("Failed to deserialize state.");
    }

    public Account SetState(Address address, object value)
    {
        var k = ToStateKey(address);
        var v = ModelSerializer.Serialize(value);
        var trie = Trie.Set(k, v);
        return new(trie);
    }

    public object? GetStateOrDefault(Address address) => TryGetState(address, out object? state) ? state : null;

    public T GetStateOrFallback<T>(Address address, T fallback)
        => GetStateOrDefault(address) is T state ? state : fallback;

    public Account RemoveState(Address address) => new(Trie.Remove(ToStateKey(address)));

    public bool TryGetState(Address address, [MaybeNullWhen(false)] out object value)
    {
        var key = ToStateKey(address);
        if (Trie.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }

        value = null;
        return false;
    }

    public bool TryGetState<T>(Address address, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetState(address, out var state) && state is T obj)
        {
            value = obj;
            return true;
        }

        value = default;
        return false;
    }
}
