using Libplanet.Node.Options;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class StoreService(IOptions<StoreOptions> storeOptions) : IStoreService
{
    private TrieStateStore? _stateStore;

    public Libplanet.Store.Repository Store { get; } = CreateStore(storeOptions.Value);

    public ITable KeyValueStore { get; } = CreateKeyValueStore(storeOptions.Value);

    public TrieStateStore StateStore => _stateStore ??= new TrieStateStore(KeyValueStore);

    private static Libplanet.Store.Repository CreateStore(StoreOptions storeOptions)
        => storeOptions.Type switch
        {
            StoreType.RocksDB => new Store.Repository(new RocksDatabase(storeOptions.StoreName)),
            StoreType.InMemory => new Libplanet.Store.Repository(new MemoryDatabase()),
            _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
        };

    private static ITable CreateKeyValueStore(StoreOptions storeOptions)
        => storeOptions.Type switch
        {
            StoreType.RocksDB => new RocksDBKeyValueStore(storeOptions.StateStoreName),
            StoreType.InMemory => new MemoryTable(),
            _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
        };
}
