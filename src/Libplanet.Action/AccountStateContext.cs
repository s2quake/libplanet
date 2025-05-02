using System.Diagnostics.CodeAnalysis;
using Libplanet.Action.State;
using Libplanet.Crypto;

namespace Libplanet.Action;

public sealed class AccountStateContext(Account account, Address address) : IAccountContext
{
    public Address Address { get; } = address;

    public bool IsReadOnly => true;

    public object this[Address address]
    {
        get => account.GetValue(address);
        set => throw new NotSupportedException("Setting state is not supported.");
    }

    public bool TryGetValue<T>(Address address, [MaybeNullWhen(false)] out T value)
        => account.TryGetValue<T>(address, out value);

    public T GetValue<T>(Address address, T fallback)
    {
        if (TryGetValue<T>(address, out var value))
        {
            return value;
        }

        return fallback;
    }

    public bool Contains(Address address) => account.GetValue(address) is not null;

    public bool Remove(Address address)
        => throw new NotSupportedException("Removing state is not supported.");
}
