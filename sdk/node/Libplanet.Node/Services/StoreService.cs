using Libplanet.Node.Options;
using Libplanet.RocksDBStore;
using Libplanet.Data;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class StoreService(IOptions<StoreOptions> storeOptions) : IStoreService
{
    public Repository Repository { get; } = CreateStore(storeOptions.Value);

    private static Repository CreateStore(StoreOptions storeOptions)
        => storeOptions.Type switch
        {
            StoreType.RocksDB => new Repository(new RocksDatabase(storeOptions.StoreName)),
            StoreType.InMemory => new Repository(new MemoryDatabase()),
            _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
        };
}
