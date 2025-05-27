using Libplanet.Data;

namespace Libplanet.Tests.Store;

public sealed class MemoryRepositoryFixture(BlockchainOptions options)
    : RepositoryFixture(new Repository(new MemoryDatabase()), options)
{
    public MemoryRepositoryFixture()
        : this(new BlockchainOptions())
    {
    }
}
