using Libplanet.Store;
using Libplanet.Store.Trie;

namespace Libplanet.Node.Services;

public interface IStoreService
{
    IStore Store { get; }

    TrieStateStore StateStore { get; }

    IKeyValueStore KeyValueStore { get; }
}
