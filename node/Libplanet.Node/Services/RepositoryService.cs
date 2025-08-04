using Libplanet.Node.Options;
using Libplanet.Data.RocksDB;
using Libplanet.Data;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class RepositoryService(IOptions<RepositoryOptions> storeOptions) : IRepositoryService
{
    public Repository Repository { get; } = CreateStore(storeOptions.Value);

    private static Repository CreateStore(RepositoryOptions storeOptions)
        => storeOptions.Type switch
        {
            // RepositoryType.Default => new Repository(new DefaultDatabase(storeOptions.Path)),
            RepositoryType.RocksDB => new Repository(new RocksDatabase(storeOptions.Path)),
            RepositoryType.InMemory => new Repository(new MemoryDatabase()),
            _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
        };
}
