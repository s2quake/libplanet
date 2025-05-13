using System.Diagnostics.CodeAnalysis;
using Libplanet.Action.State;
using Libplanet.Store.Trie;

namespace Libplanet.Action;

internal sealed class AccountContext(
    Account account, KeyBytes name, Action<AccountContext> setter) : IAccountContext
{
    private Account _account = account;

    public KeyBytes Name { get; } = name;

    public Account Account => _account;

    public bool IsReadOnly => false;

    public object this[KeyBytes key]
    {
        get => _account.GetValue(key);

        set
        {
            if (value is null)
            {
                _account = _account.RemoveValue(key);
                setter(this);
            }
            else
            {
                _account = _account.SetValue(key, value);
                setter(this);
            }
        }
    }

    public bool TryGetValue<T>(KeyBytes key, [MaybeNullWhen(false)] out T value)
        => _account.TryGetValue(key, out value);

    public T GetValue<T>(KeyBytes key, T fallback)
    {
        if (TryGetValue<T>(key, out var value))
        {
            return value;
        }

        return fallback;
    }

    public bool Contains(KeyBytes key) => _account.GetValue(key) is not null;

    public bool Remove(KeyBytes key)
    {
        if (_account.GetValue(key) is not null)
        {
            _account = _account.RemoveValue(key);
            setter(this);
            return true;
        }

        return false;
    }
}
