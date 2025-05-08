using Libplanet.Node.Options;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class StoreService(IOptions<StoreOptions> storeOptions) : IStoreService
{
    private TrieStateStore? _stateStore;

    public IStore Store { get; } = CreateStore(storeOptions.Value);

    public IDictionary<KeyBytes, byte[]> KeyValueStore { get; } = CreateKeyValueStore(storeOptions.Value);

    public TrieStateStore StateStore => _stateStore ??= new TrieStateStore(KeyValueStore);

    private static IStore CreateStore(StoreOptions storeOptions)
        => storeOptions.Type switch
        {
            StoreType.RocksDB => new RocksDBStore.RocksDBStore(storeOptions.StoreName),
            StoreType.InMemory => new MemoryStore(),
            _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
        };

    private static IDictionary<KeyBytes, byte[]> CreateKeyValueStore(StoreOptions storeOptions)
        => storeOptions.Type switch
        {
            StoreType.RocksDB => new RocksDBKeyValueStore(storeOptions.StateStoreName),
            StoreType.InMemory => new MemoryKeyValueStore(),
            _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
        };
}
