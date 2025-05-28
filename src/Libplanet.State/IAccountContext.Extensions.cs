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

    bool TryGetValue<T>(Address key, [MaybeNullWhen(false)] out T value) => TryGetValue(key.ToString(), out value);

    T GetValueOrDefault<T>(string key, T defaultValue)
        => TryGetValue<T>(key, out var value) ? value : defaultValue;

    T GetValueOrDefault<T>(Address key, T defaultValue) => GetValueOrDefault(key.ToString(), defaultValue);

    bool Contains(Address key) => Contains(key.ToString());

    bool Remove(Address key) => Remove(key.ToString());
}
