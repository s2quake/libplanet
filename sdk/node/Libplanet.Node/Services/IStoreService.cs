using Libplanet.Store;

namespace Libplanet.Node.Services;

public interface IStoreService
{
    Libplanet.Store.Repository Store { get; }

    TrieStateStore StateStore { get; }

    ITable KeyValueStore { get; }
}
