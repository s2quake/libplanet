using Libplanet.Action;
using Libplanet.Store;

namespace Libplanet.Tests.Store;

public sealed class MemoryStoreFixture : StoreFixture
{
    public MemoryStoreFixture(PolicyActions? policyActions = null)
        : base(policyActions)
    {
        Store = new MemoryStore();
        StateStore = new TrieStateStore();
    }

    protected override void Dispose(bool disposing)
    {
        Store.Dispose();
        StateStore.Dispose();
    }
}
