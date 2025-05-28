using Libplanet.Data;

namespace Libplanet.Tests.Store;

public sealed class DefaultStoreFixture(BlockchainOptions options, bool memory = true)
    : RepositoryFixture(CreateRepository(memory), options)
{
    public DefaultStoreFixture(bool memory = true)
        : this(new BlockchainOptions(), memory)
    {
    }

    private static Repository CreateRepository(bool memory)
    {
        if (memory)
        {
            return new Repository(new MemoryDatabase());
        }

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"defaultstore_test_{Guid.NewGuid()}");
        // Scheme = "default+file://";
        // var storeOptions = new DefaultStoreOptions
        // {
        //     Path = path,
        //     BlockCacheSize = 2,
        //     TxCacheSize = 2,
        // };
        return new Repository(new DefaultDatabase(path));
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
