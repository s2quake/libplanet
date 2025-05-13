using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.Trie;
using Libplanet.Types.Crypto;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action;

public partial interface IWorldContext
{
    IAccountContext this[string name] => this[ToStateKey(name)];

    IAccountContext this[Address name] => this[ToStateKey(name)];

    object this[string name, string key]
    {
        get => this[name][key];
        set => this[name][key] = value;
    }

    object this[Address name, Address key]
    {
        get => this[name][key];
        set => this[name][key] = value;
    }

    object this[KeyBytes name, KeyBytes key]
    {
        get => this[name][key];
        set => this[name][key] = value;
    }

    bool TryGetValue<T>(string name, string key, [MaybeNullWhen(false)] out T value)
        => TryGetValue(ToStateKey(name), ToStateKey(key), out value);

    bool TryGetValue<T>(Address name, Address key, [MaybeNullWhen(false)] out T value)
        => TryGetValue(ToStateKey(name), ToStateKey(key), out value);

    bool TryGetValue<T>(KeyBytes name, KeyBytes key, [MaybeNullWhen(false)] out T value)
    {
        if (this[name].TryGetValue<T>(key, out var obj))
        {
            value = obj;
            return true;
        }

        value = default;
        return false;
    }

    T GetValue<T>(string name, string key, T fallback) => GetValue(ToStateKey(name), ToStateKey(key), fallback);

    T GetValue<T>(Address name, Address key, T fallback) => GetValue(ToStateKey(name), ToStateKey(key), fallback);

    T GetValue<T>(KeyBytes name, KeyBytes key, T fallback)
    {
        if (this[name].TryGetValue<T>(key, out var obj))
        {
            return obj;
        }

        return fallback;
    }

    bool Contains(string name, string key) => Contains(ToStateKey(name), ToStateKey(key));

    bool Contains(Address name, Address key) => Contains(ToStateKey(name), ToStateKey(key));

    bool Contains(KeyBytes name, KeyBytes key) => this[name].Contains(key);

    bool Remove(string name, string key) => Remove(ToStateKey(name), ToStateKey(key));

    bool Remove(Address name, Address key) => Remove(ToStateKey(name), ToStateKey(key));

    bool Remove(KeyBytes name, KeyBytes key) => this[name].Remove(key);
}
