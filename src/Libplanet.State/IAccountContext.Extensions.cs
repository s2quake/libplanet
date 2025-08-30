using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.State;

public partial interface IAccountContext
{
    object this[Address key]
    {
        get => this[key.ToString()];
        set => this[key.ToString()] = value;
    }

    /// <exception cref="InvalidCastException">
    /// Thrown when the value stored at the specified key cannot be cast to the specified type.
    /// </exception>
    bool TryGetValue<T>(Address key, [MaybeNullWhen(false)] out T value)
        where T : notnull
        => TryGetValue(key.ToString(), out value);

    bool TryGetValueLenient<T>(string key, [MaybeNullWhen(false)] out T value)
        where T : notnull
    {
        try
        {
            if (TryGetValue<T>(key, out var v))
            {
                value = v;
                return true;
            }
        }
        catch (InvalidCastException)
        {
            // Ignored
        }

        value = default;
        return false;
    }

    /// <exception cref="InvalidCastException">
    /// Thrown when the value stored at the specified key cannot be cast to the specified type.
    /// </exception>
    T GetValueOrDefault<T>(string key, T defaultValue)
        where T : notnull
        => TryGetValue<T>(key, out var value) ? value : defaultValue;

    /// <exception cref="InvalidCastException">
    /// Thrown when the value stored at the specified key cannot be cast to the specified type.
    /// </exception>
    T GetValueOrDefault<T>(Address key, T defaultValue)
        where T : notnull
        => GetValueOrDefault(key.ToString(), defaultValue);

    T GetValueOrDefaultLenient<T>(string key, T defaultValue)
        where T : notnull
    {
        try
        {
            return GetValueOrDefault(key, defaultValue);
        }
        catch (InvalidCastException)
        {
            return defaultValue;
        }
    }

    T GetValueOrDefaultLenient<T>(Address key, T defaultValue)
        where T : notnull
        => GetValueOrDefaultLenient(key.ToString(), defaultValue);

    bool Contains(Address key) => Contains(key.ToString());

    bool Remove(Address key) => Remove(key.ToString());
}
