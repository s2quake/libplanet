using Libplanet;
using Libplanet.Data;

namespace Libplanet.Tests.Store;

public sealed class MemoryStoreFixture(BlockchainOptions options)
    : StoreFixture(new Repository(new MemoryDatabase()), options)
{
    public MemoryStoreFixture()
        : this(new BlockchainOptions())
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
