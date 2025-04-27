using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store.Trie;

namespace Libplanet.Action.State;

public interface IAccountState
{
    ITrie Trie { get; }

    IValue GetState(Address address);

    IValue[] GetStates(IEnumerable<Address> addresses);
}
