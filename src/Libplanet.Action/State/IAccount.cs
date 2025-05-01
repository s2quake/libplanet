using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store.Trie;

namespace Libplanet.Action.State;

public interface IAccount
{
    ITrie Trie { get; }

    IValue GetState(Address address);

    IValue? GetStateOrDefault(Address address) => TryGetState(address, out IValue? state) ? state : null;

    bool TryGetState(Address address, [MaybeNullWhen(false)] out IValue state);

    IAccount SetState(Address address, IValue state);

    IAccount RemoveState(Address address);
}
