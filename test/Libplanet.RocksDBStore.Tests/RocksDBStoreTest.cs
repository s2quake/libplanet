using Libplanet.Tests.Store;
using Xunit.Abstractions;

namespace Libplanet.RocksDBStore.Tests;

public class RocksDBStoreTest : StoreTest, IDisposable
{
    private readonly RocksDBStoreFixture _fx;

    public RocksDBStoreTest(ITestOutputHelper testOutputHelper)
    {
        try
        {
            TestOutputHelper = testOutputHelper;
            Fx = _fx = new RocksDBStoreFixture();
            FxConstructor = () => new RocksDBStoreFixture();
        }
        catch (TypeInitializationException)
        {
            throw new SkipException("RocksDB is not available.");
        }
    }

    protected override ITestOutputHelper TestOutputHelper { get; }

    protected override StoreFixture Fx { get; }

    protected override Func<StoreFixture> FxConstructor { get; }

    public void Dispose()
    {
        _fx?.Dispose();
    }

    // [Fact]
    // public void Loader()
    // {
    //     // TODO: Test query parameters as well.
    //     string tempDirPath = Path.GetTempFileName();
    //     File.Delete(tempDirPath);
    //     var uri = new Uri(tempDirPath, UriKind.Absolute);
    //     Assert.StartsWith("file://", uri.ToString());
    //     uri = new Uri("rocksdb+" + uri);
    //     (Libplanet.Data.Store Store, TrieStateStore StateStore)? pair = StoreLoaderAttribute.LoadStore(uri);
    //     Assert.NotNull(pair);
    //     Libplanet.Data.Store store = pair.Value.Store;
    //     Assert.IsAssignableFrom<LegacyRocksDBStore>(store);
    //     var stateStore = (TrieStateStore)pair.Value.StateStore;
    //     var kvStore = typeof(TrieStateStore)
    //         .GetProperty(
    //             "StateKeyValueStore",
    //             BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance)
    //         ?.GetMethod
    //         ?.Invoke(stateStore, Array.Empty<object>());
    //     Assert.IsAssignableFrom<RocksDBKeyValueStore>(kvStore);
    // }

    // [Fact]
    // public void ReopenStoreAfterDispose()
    // {
    //     var path = Path.Combine(Path.GetTempPath(), $"rocksdb_test_{Guid.NewGuid()}");

    //     try
    //     {
    //         var store = new Store.Store(new RocksDatabase(path));
    //         var options = new BlockChainOptions
    //         {
    //             Store = store,
    //         };
    //         _ = new BlockChain(Fx.GenesisBlock, options);
    //         store.Dispose();

    //         store = new Store.Store(new RocksDatabase(path));
    //         store.Dispose();
    //     }
    //     finally
    //     {
    //         Directory.Delete(path, true);
    //     }
    // }

    // [Fact]
    // public void ParallelGetBlock()
    // {
    //     var path = Path.Combine(Path.GetTempPath(), $"rocksdb_test_{Guid.NewGuid()}");
    //     var database = new RocksDatabase(path);
    //     var store = new Store.Store(database);
    //     try
    //     {
    //         Guid cid = Guid.NewGuid();
    //         var chain = store.GetChain(cid);
    //         // store.AppendIndex(cid, Fx.Block1.BlockHash);
    //         // store.AppendIndex(cid, Fx.Block2.BlockHash);
    //         // store.AppendIndex(cid, Fx.Block3.BlockHash);

    //         chain.Blocks.Add(Fx.Block1);
    //         chain.Blocks.Add(Fx.Block2);
    //         chain.Blocks.Add(Fx.Block3);

    //         store.Dispose();
    //         store = new Store.Store(new RocksDatabase(path));

    //         Enumerable.Range(0, 3).AsParallel().ForAll(i =>
    //         {
    //             var bHash = chain.BlockHashes[i];
    //             var block = chain.Blocks[bHash];
    //             Assert.NotNull(block);
    //         });
    //     }
    //     finally
    //     {
    //         store.Dispose();
    //         Directory.Delete(path, true);
    //     }
    // }

    // [Fact]
    // public void ListChainIds()
    // {
    //     var path = Path.Combine(Path.GetTempPath(), $"rocksdb_test_{Guid.NewGuid()}");
    //     var store = new Store.Store(new RocksDatabase(path));
    //     try
    //     {
    //         Guid cid = Guid.NewGuid();
    //         var chain = store.GetChain(cid);
    //         // store.AppendIndex(cid, Fx.Block1.BlockHash);
    //         // store.AppendIndex(cid, Fx.Block2.BlockHash);
    //         // store.AppendIndex(cid, Fx.Block3.BlockHash);

    //         chain.Blocks.Add(Fx.Block1);
    //         chain.Blocks.Add(Fx.Block2);
    //         chain.Blocks.Add(Fx.Block3);

    //         Assert.Single(store.Chains);

    //         store.ForkBlockIndexes(cid, Guid.NewGuid(), Fx.Block3.BlockHash);
    //         Assert.Equal(2, store.Chains.Count);
    //     }
    //     finally
    //     {
    //         store.Dispose();
    //         Directory.Delete(path, true);
    //     }
    // }

    // [Fact]
    // public void ParallelGetTransaction()
    // {
    //     var path = Path.Combine(Path.GetTempPath(), $"rocksdb_test_{Guid.NewGuid()}");
    //     var store = new Store.Store(new RocksDatabase(path));
    //     Transaction[] txs = new[]
    //     {
    //         Fx.Transaction1,
    //         Fx.Transaction2,
    //         Fx.Transaction3,
    //     };
    //     try
    //     {
    //         store.Transactions.Add(Fx.Transaction1);
    //         store.Transactions.Add(Fx.Transaction2);
    //         store.Transactions.Add(Fx.Transaction3);
    //         store.Dispose();
    //         store = new Store.Store(new RocksDatabase(path));

    //         Enumerable.Range(0, 3).AsParallel().ForAll(i =>
    //         {
    //             Assert.NotNull(store.Transactions[txs[i].Id]);
    //         });
    //     }
    //     finally
    //     {
    //         store.Dispose();
    //         Directory.Delete(path, true);
    //     }
    // }

    // [Fact]
    // public void PruneOutdatedChainsRocksDb()
    // {
    //     var path = Path.Combine(Path.GetTempPath(), $"rocksdb_test_{Guid.NewGuid()}");
    //     var store = new Store.Store(new RocksDatabase(path));
    //     RocksDb chainDb = null;

    //     int KeysWithChainId(RocksDb db, Guid cid)
    //     {
    //         using (Iterator it = db.NewIterator())
    //         {
    //             byte[] key = cid.ToByteArray();
    //             int count = 0;
    //             for (it.SeekToFirst(); it.Valid(); it.Next())
    //             {
    //                 if (!it.Key().Skip(1).ToArray().StartsWith(key))
    //                 {
    //                     continue;
    //                 }

    //                 count++;
    //             }

    //             return count;
    //         }
    //     }

    //     try
    //     {
    //         store.Blocks.Add(Fx.GenesisBlock);
    //         store.Blocks.Add(Fx.Block1);
    //         store.Blocks.Add(Fx.Block2);
    //         store.Blocks.Add(Fx.Block3);

    //         Guid cid1 = Guid.NewGuid();
    //         int guidLength = cid1.ToByteArray().Length;
    //         // store.AppendIndex(cid1, Fx.GenesisBlock.BlockHash);
    //         // store.AppendIndex(cid1, Fx.Block1.BlockHash);
    //         // store.AppendIndex(cid1, Fx.Block2.BlockHash);
    //         Assert.Single(store.Chains);
    //         Assert.Equal(
    //             new[] { Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash },
    //             store.GetBlockHashes(cid1).IterateIndexes(0, null));

    //         Guid cid2 = Guid.NewGuid();
    //         store.ForkBlockIndexes(cid1, cid2, Fx.Block1.BlockHash);
    //         // store.AppendIndex(cid2, Fx.Block2.BlockHash);
    //         // store.AppendIndex(cid2, Fx.Block3.BlockHash);
    //         Assert.Equal(2, store.Chains.Count);
    //         Assert.Equal(
    //             new[] { Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash, Fx.Block3.BlockHash },
    //             store.GetBlockHashes(cid2).IterateIndexes(0, null));

    //         Guid cid3 = Guid.NewGuid();
    //         store.ForkBlockIndexes(cid1, cid3, Fx.Block2.BlockHash);
    //         Assert.Equal(3, store.Chains.Count);
    //         Assert.Equal(
    //             new[] { Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash },
    //             store.GetBlockHashes(cid3).IterateIndexes(0, null));

    //         Assert.Throws<InvalidOperationException>(() => store.PruneOutdatedChains());
    //         store.PruneOutdatedChains(true);
    //         store.ChainId = cid3;
    //         store.PruneOutdatedChains();
    //         Assert.Single(store.Chains);
    //         Assert.Equal(
    //             new[] { Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash },
    //             store.GetBlockHashes(cid3).IterateIndexes(0, null));
    //         Assert.Equal(3, store.Chains[cid3].Height);

    //         store.Dispose();
    //         store = null;

    //         chainDb = RocksDb.Open(new DbOptions(), Path.Combine(path, "metadata"));

    //         Assert.Equal(0, KeysWithChainId(chainDb, cid1));
    //         Assert.Equal(0, KeysWithChainId(chainDb, cid2));
    //         Assert.NotEqual(0, KeysWithChainId(chainDb, cid3));
    //     }
    //     finally
    //     {
    //         store?.Dispose();
    //         chainDb?.Dispose();
    //         Directory.Delete(path, true);
    //     }
    // }
}
