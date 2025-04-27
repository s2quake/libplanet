using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Action;

public sealed class AccountStateContext(
    IAccountState account, Address address) : IAccountContext
{
    public Address Address { get; } = address;

    public bool IsReadOnly => true;

    public object this[Address address]
    {
        get
        {
            if (account.GetState(address) is not { } state)
            {
                throw new KeyNotFoundException($"No state found at {address}");
            }

            if (ModelSerializer.TryGetType(state, out var type))
            {
                return ModelSerializer.Deserialize(state, type)
                    ?? throw new InvalidOperationException("Failed to deserialize state.");
            }

            return state;
        }

        set => throw new NotSupportedException("Setting state is not supported.");
    }

    public bool TryGetValue<T>(Address address, [MaybeNullWhen(false)] out T value)
    {
        if (account.GetState(address) is { } state)
        {
            if (typeof(IValue).IsAssignableFrom(typeof(T)))
            {
                value = (T)state;
                return true;
            }
            else if (ModelSerializer.TryGetType(state, out var type))
            {
                if (ModelSerializer.Deserialize(state, type) is T obj)
                {
                    value = obj;
                    return true;
                }
            }
            else if (typeof(IBencodable).IsAssignableFrom(typeof(T)))
            {
                if (Activator.CreateInstance(typeof(T), args: [state]) is not T obj)
                {
                    throw new InvalidOperationException("Failed to create an instance of T.");
                }

                value = obj;
                return true;
            }
        }

        value = default;
        return false;
    }

    public T GetValue<T>(Address address, T fallback)
    {
        if (TryGetValue<T>(address, out var value))
        {
            return value;
        }

        return fallback;
    }

    public bool Contains(Address address) => account.GetState(address) is not null;

    public bool Remove(Address address)
        => throw new NotSupportedException("Removing state is not supported.");
}
