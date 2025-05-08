using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Store.Trie;

namespace Libplanet.Tests.Store;

public sealed class MemoryStoreFixture(BlockChainOptions options)
    : StoreFixture(options with { Store = new MemoryStore(), KeyValueStore = new MemoryKeyValueStore() })
{
    public MemoryStoreFixture()
        : this(new BlockChainOptions())
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
