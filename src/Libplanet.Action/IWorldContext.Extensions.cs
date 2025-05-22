using System.Diagnostics.CodeAnalysis;
using Libplanet.Types.Crypto;

namespace Libplanet.Action;

public partial interface IWorldContext
{
    IAccountContext this[Address name] => this[name.ToString()];

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

    bool TryGetValue<T>(string name, string key, [MaybeNullWhen(false)] out T value)
    {
        if (this[name].TryGetValue<T>(key, out var obj))
        {
            value = obj;
            return true;
        }

        value = default;
        return false;
    }

    bool TryGetValue<T>(Address name, Address key, [MaybeNullWhen(false)] out T value)
        => TryGetValue(name.ToString(), key.ToString(), out value);

    T GetValue<T>(string name, string key, T fallback)
    {
        if (this[name].TryGetValue<T>(key, out var obj))
        {
            return obj;
        }

        return fallback;
    }

    T GetValue<T>(Address name, Address key, T fallback) => GetValue(name.ToString(), key.ToString(), fallback);

    bool Contains(string name, string key) => this[name].Contains(key);

    bool Contains(Address name, Address key) => Contains(name.ToString(), key.ToString());

    bool Remove(string name, string key) => this[name].Remove(key);

    bool Remove(Address name, Address key) => Remove(name.ToString(), key.ToString());
}
