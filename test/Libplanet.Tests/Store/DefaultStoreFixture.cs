using Libplanet.Blockchain;
using Libplanet.Store;

namespace Libplanet.Tests.Store;

public sealed class DefaultStoreFixture(BlockChainOptions options, bool memory = true)
    : StoreFixture(CreateOptions(options, memory))
{
    public DefaultStoreFixture(bool memory = true)
        : this(new BlockChainOptions(), memory)
    {
    }

    private static BlockChainOptions CreateOptions(BlockChainOptions options, bool memory)
    {
        var path = string.Empty;
        if (!memory)
        {
            path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"defaultstore_test_{Guid.NewGuid()}");
        }

        // Scheme = "default+file://";
        // var storeOptions = new DefaultStoreOptions
        // {
        //     Path = path,
        //     BlockCacheSize = 2,
        //     TxCacheSize = 2,
        // };
        var store = new Libplanet.Store.Repository(new DefaultDatabase(path));
        return options with { Repository = store };
    }

    // public TrieStateStore LoadTrieStateStore(string path)
    // {
    //     IKeyValueStore stateKeyValueStore =
    //         new DefaultKeyValueStore(path == string.Empty
    //             ? string.Empty
    //             : System.IO.Path.Combine(path, "states"));
    //     return new TrieStateStore(stateKeyValueStore);
    // }

    protected override void Dispose(bool disposing)
    {
        // Store.Dispose();

        // if (Directory.Exists(Path))
        // {
        //     Directory.Delete(Path, true);
        // }
    }
}
