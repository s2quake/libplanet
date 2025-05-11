using Libplanet.Store;

namespace Libplanet.Node.Services;

public interface IStoreService
{
    Libplanet.Store.Store Store { get; }

    TrieStateStore StateStore { get; }

    IKeyValueStore KeyValueStore { get; }
}
