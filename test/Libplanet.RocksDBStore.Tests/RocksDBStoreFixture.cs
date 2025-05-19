using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Tests.Store;

namespace Libplanet.RocksDBStore.Tests;

public class RocksDBStoreFixture : StoreFixture
{
    public RocksDBStoreFixture(BlockChainOptions? options = null)
        : base(CreateOptions(options ?? new BlockChainOptions()))
    {
        // Path = System.IO.Path.Combine(
        //     System.IO.Path.GetTempPath(),
        //     $"rocksdb_test_{Guid.NewGuid()}");

        // Scheme = "rocksdb+file://";

        // var store = new RocksDBStore(Path, blockCacheSize: 2, txCacheSize: 2);
        // Store = store;
        // StateStore = LoadTrieStateStore(Path);
    }

    private static BlockChainOptions CreateOptions(BlockChainOptions options)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rocksdb_test_{Guid.NewGuid()}");
        var store = new Libplanet.Store.Repository(new RocksDatabase(path));
        return options with { Repository = store };
    }

    public TrieStateStore LoadTrieStateStore(string path)
    {
        var stateKeyValueStore =
            new RocksDBKeyValueStore(System.IO.Path.Combine(path, "states"));
        return new TrieStateStore(stateKeyValueStore);
    }

    protected override void Dispose(bool disposing)
    {
        // Store?.Dispose();
        // StateStore?.Dispose();

        // if (!(Path is null))
        // {
        //     Directory.Delete(Path, true);
        // }
    }
}
