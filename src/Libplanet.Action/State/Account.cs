using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public sealed record class Account(ITrie Trie) : IAccount
{
    public IValue GetState(Address address) => Trie[ToStateKey(address)];

    public Account SetState(Address address, IValue state) => new(new Account(Trie.Set(ToStateKey(address), state)));

    public Account RemoveState(Address address) => new(new Account(Trie.Remove(ToStateKey(address))));

    public bool TryGetState(Address address, [MaybeNullWhen(false)] out IValue state)
    {
        if (Trie.TryGetValue(ToStateKey(address), out var value))
        {
            state = value;
            return true;
        }

        state = null;
        return false;
    }

    IAccount IAccount.SetState(Address address, IValue state) => SetState(address, state);

    IAccount IAccount.RemoveState(Address address) => RemoveState(address);
}
