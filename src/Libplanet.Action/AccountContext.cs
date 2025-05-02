using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Action;

internal sealed class AccountContext(
    Account account, Address address, Action<AccountContext> setter) : IAccountContext
{
    private Account _account = account;

    public Address Address { get; } = address;

    public Account Account => _account;

    public bool IsReadOnly => false;

    public object this[Address address]
    {
        get => _account.GetValue(address);

        set
        {
            if (value is null)
            {
                _account = _account.RemoveValue(address);
                setter(this);
            }
            else
            {
                _account = _account.SetValue(address, value);
                setter(this);
            }
        }
    }

    public bool TryGetValue<T>(Address address, [MaybeNullWhen(false)] out T value)
        => _account.TryGetValue<T>(address, out value);

    public T GetValue<T>(Address address, T fallback)
    {
        if (TryGetValue<T>(address, out var value))
        {
            return value;
        }

        return fallback;
    }

    public bool Contains(Address address) => _account.GetValue(address) is not null;

    public bool Remove(Address address)
    {
        if (_account.GetValue(address) is not null)
        {
            _account = _account.RemoveValue(address);
            setter(this);
            return true;
        }

        return false;
    }
}
