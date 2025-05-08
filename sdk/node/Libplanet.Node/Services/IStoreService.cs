using Libplanet.Store;
using Libplanet.Store.Trie;

namespace Libplanet.Node.Services;

public interface IStoreService
{
    IStore Store { get; }

    TrieStateStore StateStore { get; }

    IDictionary<KeyBytes, byte[]> KeyValueStore { get; }
}
