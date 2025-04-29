using Libplanet.Action;
using Libplanet.Tests.Blockchain;
using Libplanet.Tests.Store;
using Xunit.Abstractions;

namespace Libplanet.RocksDBStore.Tests
{
    public class RocksDBStoreBlockChainTest : BlockChainTest
    {
        public RocksDBStoreBlockChainTest(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override StoreFixture GetStoreFixture(
            PolicyActions policyActions = null)
        {
            try
            {
                return new RocksDBStoreFixture(
                    policyActions);
            }
            catch (TypeInitializationException)
            {
                throw new SkipException("RocksDB is not available.");
            }
        }
    }
}
