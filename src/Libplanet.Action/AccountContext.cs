using System.Diagnostics.CodeAnalysis;
using Libplanet.Action.State;
using Libplanet.Data.Structures;

namespace Libplanet.Action;

internal sealed class AccountContext(Account account, string name, Action<AccountContext> setter)
    : IAccountContext
{
    private Account _account = account;

    public string Name { get; } = name;

    public Account Account => _account;

    public object this[string key]
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

    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
        => _account.TryGetValue(key, out value);

    public T GetValue<T>(string key, T fallback) => TryGetValue<T>(key, out var value) ? value : fallback;

    public bool Contains(string key) => _account.GetValueOrDefault(key) is not null;

    public bool Remove(string key)
    {
        if (_account.GetValueOrDefault(key) is not null)
        {
            _account = _account.RemoveValue(key);
            setter(this);
            return true;
        }

        return false;
    }
}
