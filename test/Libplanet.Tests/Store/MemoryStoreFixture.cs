using Libplanet.Action;
using Libplanet.Store;

namespace Libplanet.Tests.Store;

public class MemoryStoreFixture : StoreFixture
{
    public MemoryStoreFixture(
        PolicyActions? policyActions = null)
        : base(policyActions)
    {
        Store = new MemoryStore();
        StateStore = new TrieStateStore();
    }

    public override void Dispose()
    {
        Store.Dispose();
        StateStore.Dispose();
    }
}
