using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Data.Structures;

public partial interface ITrie
{
    bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetValue(key, out object? v) && v is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    T? GetValueOrDefault<T>(string key)
        => TryGetValue(key, out object? value) && value is T t ? t : default;

    T GetValueOrDefault<T>(string key, T defaultValue)
        => TryGetValue(key, out object? value) && value is T t ? t : defaultValue;
}
