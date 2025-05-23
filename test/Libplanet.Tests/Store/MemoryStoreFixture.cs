using Libplanet.Blockchain;
using Libplanet.Data;

namespace Libplanet.Tests.Store;

public sealed class MemoryStoreFixture(BlockChainOptions options)
    : StoreFixture(new Repository(new MemoryDatabase()), options)
{
    public MemoryStoreFixture()
        : this(new BlockChainOptions())
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
