using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public sealed record class Account : IAccount
{
    private readonly IAccountState _state;

    public Account(IAccountState state)
    {
        _state = state;
    }

    public ITrie Trie => _state.Trie;

    public IValue GetState(Address address) => _state.GetState(address);

    public IValue[] GetStates(IEnumerable<Address> addresses) => _state.GetStates(addresses);

    public IAccount SetState(Address address, IValue state) => UpdateState(address, state);

    public IAccount RemoveState(Address address) => UpdateState(address, null);

    private Account UpdateState(
        Address address,
        IValue? value) => value is { } v
            ? new Account(new AccountState(Trie.Set(ToStateKey(address), v)))
            : new Account(new AccountState(Trie.Remove(ToStateKey(address))));

    public bool TryGetState(Address address, [MaybeNullWhen(false)] out IValue state)
        => _state.TryGetState(address, out state);
}
