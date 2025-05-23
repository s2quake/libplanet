using Libplanet;
using Libplanet.Tests.Blockchain;
using Libplanet.Tests.Store;
using Xunit.Abstractions;

namespace Libplanet.RocksDBStore.Tests;

public class RocksDBStoreBlockChainTest : BlockChainTest
{
    public RocksDBStoreBlockChainTest(ITestOutputHelper output)
        : base(output)
    {
    }

    protected override StoreFixture GetStoreFixture(BlockchainOptions? options = null)
    {
        try
        {
            return new RocksDBStoreFixture(options);
        }
        catch (TypeInitializationException)
        {
            throw new SkipException("RocksDB is not available.");
        }
    }
}
