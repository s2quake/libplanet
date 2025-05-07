using Libplanet.Store;
using Libplanet.Store.Trie;

namespace Libplanet.Tests.Store;

public sealed class MemoryStoreFixture : StoreFixture
{
    public MemoryStoreFixture()
        : base(new MemoryStore(), new MemoryKeyValueStore())
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
