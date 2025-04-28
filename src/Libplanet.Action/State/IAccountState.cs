using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store.Trie;

namespace Libplanet.Action.State;

public interface IAccountState
{
    ITrie Trie { get; }

    IValue GetState(Address address);

    bool TryGetState(Address address, [MaybeNullWhen(false)] out IValue state);

    IValue[] GetStates(IEnumerable<Address> addresses);
}
