using Libplanet.Crypto;
using Libplanet.Store.Trie;

namespace Libplanet.Action.State;

public interface IWorldState
{
    public ITrie Trie { get; }

    bool Legacy { get; }

    int Version { get; }

    IAccountState GetAccountState(Address address);
}
