using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Tests.Store;

namespace Libplanet.RocksDBStore.Tests;

public class RocksDBStoreFixture : StoreFixture
{
    public RocksDBStoreFixture(BlockChainOptions? options = null)
        : base(CreateOptions(), options ?? new BlockChainOptions())
    {
        // Path = System.IO.Path.Combine(
        //     System.IO.Path.GetTempPath(),
        //     $"rocksdb_test_{Guid.NewGuid()}");

        // Scheme = "rocksdb+file://";

        // var store = new RocksDBStore(Path, blockCacheSize: 2, txCacheSize: 2);
        // Store = store;
        // StateStore = LoadTrieStateStore(Path);
    }

    private static Repository CreateOptions()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rocksdb_test_{Guid.NewGuid()}");
        return new Repository(new RocksDatabase(path));
    }

    public StateStore LoadTrieStateStore(string path)
    {
        var stateKeyValueStore =
            new RocksDBKeyValueStore(System.IO.Path.Combine(path, "states"));
        return new StateStore(stateKeyValueStore);
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
