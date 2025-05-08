using System.IO;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Tests.Store;

namespace Libplanet.RocksDBStore.Tests;

public class RocksDBStoreFixture : StoreFixture
{
    public RocksDBStoreFixture(
        BlockChainOptions? options = null)
        : base(options ?? new BlockChainOptions())
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"rocksdb_test_{Guid.NewGuid()}");

        Scheme = "rocksdb+file://";

        var store = new RocksDBStore(Path, blockCacheSize: 2, txCacheSize: 2);
        // Store = store;
        // StateStore = LoadTrieStateStore(Path);
    }

    public TrieStateStore LoadTrieStateStore(string path)
    {
        IKeyValueStore stateKeyValueStore =
            new RocksDBKeyValueStore(System.IO.Path.Combine(path, "states"));
        return new TrieStateStore(stateKeyValueStore);
    }

    protected override void Dispose(bool disposing)
    {
        // Store?.Dispose();
        // StateStore?.Dispose();

        if (!(Path is null))
        {
            Directory.Delete(Path, true);
        }
    }
}
