using Libplanet.Node.Options;
using Libplanet.Data.RocksDB;
using Libplanet.Data;
using Microsoft.Extensions.Options;
using Libplanet.Data.LiteDB;

namespace Libplanet.Node.Services;

internal sealed class RepositoryService(IOptions<RepositoryOptions> repositoryOptions) : IRepositoryService
{
    public Repository Repository { get; } = CreateStore(repositoryOptions.Value);

    public RepositoryType Type => repositoryOptions.Value.Type;

    private static Repository CreateStore(RepositoryOptions storeOptions) => storeOptions.Type switch
    {
        RepositoryType.RocksDB => new Repository(new RocksDatabase(storeOptions.Path)),
        RepositoryType.Memory => new Repository(new MemoryDatabase()),
        RepositoryType.LiteDB => new Repository(new LiteDatabase(storeOptions.Path)),
        _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
    };
}
