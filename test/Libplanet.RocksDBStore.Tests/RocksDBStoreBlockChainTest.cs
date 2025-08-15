using Libplanet.Tests.Blockchain;
using Libplanet.Tests.Store;
using Xunit.Abstractions;

namespace Libplanet.Data.RocksDB.Tests;

public class RocksDBStoreBlockChainTest : BlockchainTest
{
    public RocksDBStoreBlockChainTest(ITestOutputHelper output)
        : base(output)
    {
    }

    protected override RepositoryFixture GetStoreFixture(BlockchainOptions? options = null)
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
