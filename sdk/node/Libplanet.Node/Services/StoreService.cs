using Libplanet.Node.Options;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class StoreService(IOptions<StoreOptions> storeOptions) : IStoreService
{
    private TrieStateStore? _stateStore;

    public Libplanet.Store.Store Store { get; } = CreateStore(storeOptions.Value);

    public IKeyValueStore KeyValueStore { get; } = CreateKeyValueStore(storeOptions.Value);

    public TrieStateStore StateStore => _stateStore ??= new TrieStateStore(KeyValueStore);

    private static Libplanet.Store.Store CreateStore(StoreOptions storeOptions)
        => storeOptions.Type switch
        {
            StoreType.RocksDB => new Store.Store(new RocksDatabase(storeOptions.StoreName)),
            StoreType.InMemory => new Libplanet.Store.Store(new MemoryDatabase()),
            _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
        };

    private static IKeyValueStore CreateKeyValueStore(StoreOptions storeOptions)
        => storeOptions.Type switch
        {
            StoreType.RocksDB => new RocksDBKeyValueStore(storeOptions.StateStoreName),
            StoreType.InMemory => new MemoryKeyValueStore(),
            _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
        };
}
