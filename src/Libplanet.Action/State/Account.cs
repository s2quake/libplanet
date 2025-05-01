using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public sealed record class Account : IAccount
{
    public Account(ITrie trie)
    {
        Trie = trie;
    }

    public ITrie Trie { get; }

    public IValue GetState(Address address) => Trie[ToStateKey(address)];

    public Account SetState(Address address, IValue state) => UpdateState(address, state);

    public Account RemoveState(Address address) => UpdateState(address, null);

    private Account UpdateState(
        Address address,
        IValue? value) => value is { } v
            ? new Account(new Account(Trie.Set(ToStateKey(address), v)))
            : new Account(new Account(Trie.Remove(ToStateKey(address))));

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

    IAccount IAccount.SetState(Address address, IValue state)
    {
        return SetState(address, state);
    }

    IAccount IAccount.RemoveState(Address address)
    {
        return RemoveState(address);
    }
}
