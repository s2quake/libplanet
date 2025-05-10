using Libplanet.Store;
using Libplanet.Store.Trie;

namespace Libplanet.Node.Services;

public interface IStoreService
{
    Libplanet.Store.Store Store { get; }

    TrieStateStore StateStore { get; }

    IKeyValueStore KeyValueStore { get; }
}
