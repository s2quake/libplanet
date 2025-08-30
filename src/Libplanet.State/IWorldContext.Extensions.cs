using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.State;

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

    /// <exception cref="InvalidCastException">
    /// Thrown when the value stored at the specified key cannot be cast to the specified type.
    /// </exception>
    bool TryGetValue<T>(string name, string key, [MaybeNullWhen(false)] out T value)
        where T : notnull
    {
        if (this[name].TryGetValue<T>(key, out var obj))
        {
            value = obj;
            return true;
        }

        value = default;
        return false;
    }

    /// <exception cref="InvalidCastException">
    /// Thrown when the value stored at the specified key cannot be cast to the specified type.
    /// </exception>
    bool TryGetValue<T>(Address name, Address key, [MaybeNullWhen(false)] out T value)
        where T : notnull
        => TryGetValue(name.ToString(), key.ToString(), out value);

    bool TryGetValueLenient<T>(string name, string key, [MaybeNullWhen(false)] out T value)
        where T : notnull
        => this[name].TryGetValueLenient(key, out value);

    bool TryGetValueLenient<T>(string name, Address key, [MaybeNullWhen(false)] out T value)
        where T : notnull
        => TryGetValueLenient(name, key.ToString(), out value);

    /// <exception cref="InvalidCastException">
    /// Thrown when the value stored at the specified key cannot be cast to the specified type.
    /// </exception>
    T GetValueOrDefault<T>(string name, string key, T defaultValue)
        where T : notnull
    {
        if (this[name].TryGetValue<T>(key, out var obj))
        {
            return obj;
        }

        return defaultValue;
    }

    /// <exception cref="InvalidCastException">
    /// Thrown when the value stored at the specified key cannot be cast to the specified type.
    /// </exception>
    T GetValueOrDefault<T>(Address name, Address key, T defaultValue)
        where T : notnull
        => GetValueOrDefault(name.ToString(), key.ToString(), defaultValue);

    T GetValueOrDefaultLenient<T>(string name, string key, T defaultValue)
        where T : notnull
        => this[name].GetValueOrDefaultLenient(key, defaultValue);

    T GetValueOrDefaultLenient<T>(Address name, Address key, T defaultValue)
        where T : notnull
        => GetValueOrDefaultLenient(name.ToString(), key.ToString(), defaultValue);

    bool Contains(string name, string key) => this[name].Contains(key);

    bool Contains(Address name, Address key) => Contains(name.ToString(), key.ToString());

    bool Remove(string name, string key) => this[name].Remove(key);

    bool Remove(Address name, Address key) => Remove(name.ToString(), key.ToString());
}
