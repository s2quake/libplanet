using System.Diagnostics.CodeAnalysis;
using Libplanet.Types.Crypto;

namespace Libplanet.Action;

public partial interface IAccountContext
{
    object this[Address key]
    {
        get => this[key.ToString()];
        set => this[key.ToString()] = value;
    }

    bool TryGetValue<T>(Address key, [MaybeNullWhen(false)] out T value) => TryGetValue(key.ToString(), out value);

    T GetValue<T>(Address key, T fallback) => GetValue(key.ToString(), fallback);

    bool Contains(Address key) => Contains(key.ToString());

    bool Remove(Address key) => Remove(key.ToString());
}
