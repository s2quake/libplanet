using Libplanet.Crypto;
using Libplanet.Store.Trie;

namespace Libplanet.Action.State;

public interface IWorld
{
    ITrie Trie { get; }

    int Version => Trie.GetMetadata() is { } value ? value.Version : 0;

    ImmutableDictionary<Address, IAccount> Delta { get; }

    // bool IsReadOnly { get; }

    IAccount GetAccount(Address address);

    IWorld SetAccount(Address address, IAccount account);
}
