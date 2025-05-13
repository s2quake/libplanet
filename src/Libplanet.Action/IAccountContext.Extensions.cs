using System.Diagnostics.CodeAnalysis;
using Libplanet.Action.State;
using Libplanet.Store.Trie;
using Libplanet.Types.Crypto;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action;

public partial interface IAccountContext
{
    object this[string key]
    {
        get => this[ToStateKey(key)];
        set => this[ToStateKey(key)] = value;
    }

    object this[Address key]
    {
        get => this[ToStateKey(key)];
        set => this[ToStateKey(key)] = value;
    }

    bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value) => TryGetValue(ToStateKey(key), out value);

    bool TryGetValue<T>(Address key, [MaybeNullWhen(false)] out T value) => TryGetValue(ToStateKey(key), out value);

    T GetValue<T>(string key, T fallback) => GetValue(ToStateKey(key), fallback);

    T GetValue<T>(Address key, T fallback) => GetValue(ToStateKey(key), fallback);

    bool Contains(string key) => Contains(ToStateKey(key));

    bool Contains(Address key) => Contains(ToStateKey(key));

    bool Remove(string key) => Remove(ToStateKey(key));

    bool Remove(Address key) => Remove(ToStateKey(key));
}
