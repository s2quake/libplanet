using Libplanet.Tests.Store;

namespace Libplanet.Data.RocksDB.Tests;

public class RocksDBStoreFixture : RepositoryFixture
{
    public RocksDBStoreFixture(BlockchainOptions? options = null)
        : base(CreateOptions(), options ?? new BlockchainOptions())
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

    public StateIndex LoadTrieStateStore(string path)
    {
        var stateKeyValueStore =
            new RocksTable(System.IO.Path.Combine(path, "states"));
        return new StateIndex(stateKeyValueStore);
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
