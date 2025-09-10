using global::Cocona;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet.Commands;

public class StoreCommand
{
    private const string StoreArgumentDescription =
        "The URI denotes the type and path of concrete class for " + nameof(Libplanet.Data.Repository) + "."
        + "<store-type>://<store-path> (e.g., rocksdb+file:///path/to/store)";

    [Command(Description = "Build an index for transaction id and block hash.")]
    public void BuildIndexTxBlock(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("OFFSET", Description = "block height")]
        int offset,
        [Argument("LIMIT", Description = "block height")]
        int limit)
    {
        var repository = Utils.LoadStoreFromUri(home);
        using var _ = new RepositoryDiposer(repository);
        var prev = DateTimeOffset.UtcNow;
        foreach (var index in BuildTxIdBlockHashIndex(repository, offset, limit))
        {
            Console.WriteLine($"processing {index}/{offset + limit}...");
        }

        Console.WriteLine($"It taken {DateTimeOffset.UtcNow - prev}");
    }

    [Command(Description = "Query block hashes by transaction id.")]
    public void BlockHashesByTxId(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("TX-ID", Description = "tx id")]
        string strTxId)
    {
        var repository = Utils.LoadStoreFromUri(home);
        using var _ = new RepositoryDiposer(repository);
        // var blockHashes = store.TxExecutions[new TxId(ByteUtility.ParseHex(strTxId))].Select(item => item.BlockHash);
        // Console.WriteLine(Utils.SerializeHumanReadable(blockHashes));
    }

    [Command(Description = "Query a list of blocks by transaction id.")]
    public void BlocksByTxId(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("TX-ID", Description = "tx id")]
        string strTxId)
    {
        // using Libplanet.Data.Store store = Utils.LoadStoreFromUri(home);
        var repository = new Libplanet.Data.Repository(new MemoryDatabase());
        using var _ = new RepositoryDiposer(repository);
        var txId = TxId.Parse(strTxId);
        // if (!store.TxExecutions.TryGetValue(txId, out var txExecutions) || txExecutions.Length == 0)
        // {
        //     throw Utils.Error($"cannot find the block with the TxId[{txId.ToString()}]");
        // }

        var blocks = IterateBlocks(repository, txId).ToImmutableList();

        Console.WriteLine(Utils.SerializeHumanReadable(blocks));
    }

    [Command(Description = "Query a block by index.")]
    public void BlockByIndex(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("BLOCK-INDEX", Description = "block height")]
        int blockHeight)
    {
        // using Libplanet.Data.Store store = Utils.LoadStoreFromUri(home);
        var repository = new Repository(new MemoryDatabase());
        var blockHash = GetBlockHash(repository, blockHeight);
        var block = GetBlock(repository, blockHash);
        Console.WriteLine(Utils.SerializeHumanReadable(block));
    }

    [Command(Description = "Query a block by hash.")]
    public void BlockByHash(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("BLOCK-HASH", Description = "block hash")]
        string blockHash)
    {
        // using Libplanet.Data.Store store = Utils.LoadStoreFromUri(home);
        Libplanet.Data.Repository store = new Libplanet.Data.Repository(new MemoryDatabase());
        var block = GetBlock(store, BlockHash.Parse(blockHash));
        Console.WriteLine(Utils.SerializeHumanReadable(block));
    }

    [Command(Description = "Query a transaction by tx id.")]
    public void TxById(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("TX-ID", Description = "tx id")]
        string strTxId)
    {
        var repository = Utils.LoadStoreFromUri(home);
        using var _ = new RepositoryDiposer(repository);
        var tx = GetTransaction(repository, new TxId(ByteUtility.ParseHex(strTxId)));
        Console.WriteLine(Utils.SerializeHumanReadable(tx));
    }

    [Command]
    public void MigrateIndex(string storePath)
    {
        throw new NotImplementedException();
        // if (RocksDBStore.LegacyRocksDBStore.MigrateChainDBFromColumnFamilies(storePath))
        // {
        //     Console.WriteLine("Successfully migrated.");
        // }
        // else
        // {
        //     Console.WriteLine("Already migrated, no need to migrate.");
        // }
    }

    private static Block GetBlock(Libplanet.Data.Repository store, BlockHash blockHash)
    {
        if (!store.TryGetBlock(blockHash, out var block))
        {
            throw Utils.Error($"cannot find the block with the hash[{blockHash.ToString()}]");
        }

        return block;
    }

    private static BlockHash GetBlockHash(Libplanet.Data.Repository store, int blockHeight)
    {
        var chain = store;
        if (!chain.BlockHashes.TryGetValue(blockHeight, out var blockHash))
        {
            throw Utils.Error(
                $"Cannot find the block with the height {blockHeight}" +
                $" within the blockchain {store}.");
        }

        return blockHash;
    }

    private static IEnumerable<Block> IterateBlocks(Libplanet.Data.Repository store, TxId txId)
    {
        yield break;
        // foreach (var txExecution in store.TxExecutions[txId])
        // {
        //     yield return GetBlock(store, txExecution.BlockHash);
        // }
    }

    private static Transaction GetTransaction(Libplanet.Data.Repository store, TxId txId)
    {
        if (!store.PendingTransactions.TryGetValue(txId, out var tx))
        {
            throw Utils.Error($"cannot find the tx with the tx id[{txId.ToString()}]");
        }

        return tx;
    }

    private static IEnumerable<int> BuildTxIdBlockHashIndex(Libplanet.Data.Repository store, int offset, int limit)
    {
        throw new NotImplementedException("This method cannot be used");
        // if (store.ChainId == Guid.Empty)
        // {
        //     throw Utils.Error("Cannot find the main branch of the blockchain.");
        // }

        // var index = offset;
        // var chain = store.Chain;
        // foreach (BlockHash blockHash in chain.BlockHashes.IterateIndexes(offset, limit))
        // {
        //     yield return index++;
        //     if (!store.BlockDigests.TryGetValue(blockHash, out var blockDigest))
        //     {
        //         throw Utils.Error(
        //             $"Block is missing for BlockHash: {blockHash} index: {index}.");
        //     }

        //     foreach (var txId in blockDigest.TxIds)
        //     {
        //         store.TxExecutions.Add(txId, blockHash);
        //     }
        // }
    }

    private sealed class RepositoryDiposer(Repository repository) : IDisposable
    {
        private readonly IDisposable? _repository = repository as IDisposable;

        public void Dispose()
        {
            _repository?.Dispose();
        }
    }
}
