using System.Diagnostics.CodeAnalysis;
using Libplanet.Action.State;
using Libplanet.Store.Trie;

namespace Libplanet.Action;

public sealed class AccountStateContext(Account account, KeyBytes name) : IAccountContext
{
    public KeyBytes Name { get; } = name;

    public bool IsReadOnly => true;

    public object this[KeyBytes key]
    {
        get => account.GetValue(key);
        set => throw new NotSupportedException("Setting state is not supported.");
    }

    public bool TryGetValue<T>(KeyBytes key, [MaybeNullWhen(false)] out T value)
        => account.TryGetValue(key, out value);

    public T GetValue<T>(KeyBytes key, T fallback)
    {
        if (TryGetValue<T>(key, out var value))
        {
            return value;
        }

        return fallback;
    }

    public bool Contains(KeyBytes key) => account.GetValue(key) is not null;

    public bool Remove(KeyBytes key)
        => throw new NotSupportedException("Removing state is not supported.");
}
