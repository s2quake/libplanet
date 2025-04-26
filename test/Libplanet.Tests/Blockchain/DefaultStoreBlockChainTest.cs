using Libplanet.Action;
using Libplanet.Tests.Store;
using Xunit.Abstractions;

namespace Libplanet.Tests.Blockchain
{
    public class DefaultStoreBlockChainTest : BlockChainTest
    {
        public DefaultStoreBlockChainTest(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override StoreFixture GetStoreFixture(
            PolicyActions policyActions = null) =>
                new DefaultStoreFixture(policyActions: policyActions);
    }
}
