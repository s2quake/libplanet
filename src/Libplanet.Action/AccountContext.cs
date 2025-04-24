using System.Diagnostics.CodeAnalysis;
using Bencodex;
using Bencodex.Types;
using Libplanet.Serialization;
using Libplanet.Action.State;
using Libplanet.Crypto;

namespace Libplanet.Action;

internal sealed class AccountContext(
    IAccount account, Address address, Action<AccountContext> setter) : IAccountContext
{
    private IAccount _account = account;

    public Address Address { get; } = address;

    public IAccount Account => _account;

    public bool IsReadOnly => false;

    public object this[Address address]
    {
        get
        {
            if (_account.GetState(address) is not { } state)
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

        set
        {
            if (value is null)
            {
                _account = _account.RemoveState(address);
                setter(this);
            }
            else if (value is IValue state)
            {
                _account = _account.SetState(address, state);
                setter(this);
            }
            else if (value is IBencodable obj)
            {
                if (obj is IValidateState validateState)
                {
                    validateState.Validate();
                }

                _account = _account.SetState(address, obj.Bencoded);
                setter(this);
            }
            else if (ModelSerializer.CanSupportType(value.GetType()))
            {
                _account = _account.SetState(address, ModelSerializer.Serialize(value));
                setter(this);
            }
            else
            {
                throw new NotSupportedException("Setting state is not supported.");
            }
        }
    }

    public bool TryGetValue<T>(Address address, [MaybeNullWhen(false)] out T value)
    {
        if (_account.GetState(address) is { } state)
        {
            if (ModelSerializer.TryGetType(state, out var type))
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
            else if (typeof(IValue).IsAssignableFrom(typeof(T)))
            {
                value = (T)state;
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

    public bool Contains(Address address) => _account.GetState(address) is not null;

    public bool Remove(Address address)
    {
        if (_account.GetState(address) is not null)
        {
            _account = _account.RemoveState(address);
            setter(this);
            return true;
        }

        return false;
    }
}
