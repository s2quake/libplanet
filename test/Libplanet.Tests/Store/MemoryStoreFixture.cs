using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Store.Trie;

namespace Libplanet.Tests.Store;

public sealed class MemoryStoreFixture(BlockChainOptions options)
    : StoreFixture(CreateOptions(options))
{
    public MemoryStoreFixture()
        : this(new BlockChainOptions())
    {
    }

    private static BlockChainOptions CreateOptions(BlockChainOptions options)
    {
        var store = new Libplanet.Store.Store(new MemoryDatabase());
        return options with { Store = store };
    }

    protected override void Dispose(bool disposing)
    {
    }
}
