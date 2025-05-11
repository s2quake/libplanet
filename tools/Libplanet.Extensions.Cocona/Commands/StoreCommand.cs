using System.Globalization;
using global::Cocona;
using Libplanet.Store;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Extensions.Cocona.Commands;

public class StoreCommand
{
    private const string StoreArgumentDescription =
        "The URI denotes the type and path of concrete class for " + nameof(Libplanet.Store.Store) + "."
        + "<store-type>://<store-path> (e.g., rocksdb+file:///path/to/store)";

    [Command(Description = "List all chain IDs.")]
    public void ChainIds(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string storeUri,
        [Option("hash", Description = "Show the hash of the chain tip.")]
        bool showHash)
    {
        Libplanet.Store.Store store = Utils.LoadStoreFromUri(storeUri);
        Guid? canon = store.GetCanonicalChainId();
        var headerWithoutHash = ("Chain ID", "Height", "Canon?");
        var headerWithHash = ("Chain ID", "Height", "Canon?", "Hash");
        var chainIds = store.ListChainIds().Select(id =>
        {
            var height = store.CountIndex(id) - 1;
            return (
                id.ToString(),
                height.ToString(CultureInfo.InvariantCulture),
                id == canon ? "*" : string.Empty,
                store.GetBlockDigest(
                    store.GetBlockHash(id, height))!.BlockHash.ToString());
        });
        if (showHash)
        {
            Utils.PrintTable(headerWithHash, chainIds);
        }
        else
        {
            Utils.PrintTable(
                headerWithoutHash, chainIds.Select(i => (i.Item1, i.Item2, i.Item3)));
        }
    }

    [Command(Description = "Build an index for transaction id and block hash.")]
    public void BuildIndexTxBlock(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("OFFSET", Description = "block height")]
        int offset,
        [Argument("LIMIT", Description = "block height")]
        int limit)
    {
        Libplanet.Store.Store store = Utils.LoadStoreFromUri(home);
        var prev = DateTimeOffset.UtcNow;
        foreach (var index in BuildTxIdBlockHashIndex(store, offset, limit))
        {
            Console.WriteLine($"processing {index}/{offset + limit}...");
        }

        Console.WriteLine($"It taken {DateTimeOffset.UtcNow - prev}");
        store?.Dispose();
    }

    [Command(Description = "Query block hashes by transaction id.")]
    public void BlockHashesByTxId(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("TX-ID", Description = "tx id")]
        string strTxId)
    {
        Libplanet.Store.Store store = Utils.LoadStoreFromUri(home);
        var blockHashes = store.IterateTxIdBlockHashIndex(new TxId(ByteUtility.ParseHex(strTxId)))
            .ToImmutableArray();
        Console.WriteLine(Utils.SerializeHumanReadable(blockHashes));
        store?.Dispose();
    }

    [Command(Description = "Query a list of blocks by transaction id.")]
    public void BlocksByTxId(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("TX-ID", Description = "tx id")]
        string strTxId)
    {
        // using Libplanet.Store.Store store = Utils.LoadStoreFromUri(home);
        Libplanet.Store.Store store = new Libplanet.Store.Store(new MemoryDatabase());
        var txId = TxId.Parse(strTxId);
        if (!(store.GetFirstTxIdBlockHashIndex(txId) is { }))
        {
            throw Utils.Error($"cannot find the block with the TxId[{txId.ToString()}]");
        }

        var blocks = IterateBlocks(store, txId).ToImmutableList();

        Console.WriteLine(Utils.SerializeHumanReadable(blocks));
    }

    [Command(Description = "Query a block by index.")]
    public void BlockByIndex(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("BLOCK-INDEX", Description = "block height")]
        int blockHeight)
    {
        // using Libplanet.Store.Store store = Utils.LoadStoreFromUri(home);
        Libplanet.Store.Store store = new Libplanet.Store.Store(new MemoryDatabase());
        var blockHash = GetBlockHash(store, blockHeight);
        var block = GetBlock(store, blockHash);
        Console.WriteLine(Utils.SerializeHumanReadable(block));
    }

    [Command(Description = "Query a block by hash.")]
    public void BlockByHash(
        [Argument("STORE", Description = StoreArgumentDescription)]
        string home,
        [Argument("BLOCK-HASH", Description = "block hash")]
        string blockHash)
    {
        // using Libplanet.Store.Store store = Utils.LoadStoreFromUri(home);
        Libplanet.Store.Store store = new Libplanet.Store.Store(new MemoryDatabase());
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
        Libplanet.Store.Store store = Utils.LoadStoreFromUri(home);
        var tx = GetTransaction(store, new TxId(ByteUtility.ParseHex(strTxId)));
        Console.WriteLine(Utils.SerializeHumanReadable(tx));
        store?.Dispose();
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

    private static Block GetBlock(Libplanet.Store.Store store, BlockHash blockHash)
    {
        if (!(store.GetBlock(blockHash) is { } block))
        {
            throw Utils.Error($"cannot find the block with the hash[{blockHash.ToString()}]");
        }

        return block;
    }

    private static BlockHash GetBlockHash(Libplanet.Store.Store store, int blockHeight)
    {
        if (!(store.GetCanonicalChainId() is { } chainId))
        {
            throw Utils.Error("Cannot find the main branch of the blockchain.");
        }

        if (!(store.GetBlockHash(chainId, blockHeight) is { } blockHash))
        {
            throw Utils.Error(
                $"Cannot find the block with the height {blockHeight}" +
                $" within the blockchain {chainId}.");
        }

        return blockHash;
    }

    private static IEnumerable<Block> IterateBlocks(Libplanet.Store.Store store, TxId txId)
    {
        foreach (var blockHash in store.IterateTxIdBlockHashIndex(txId))
        {
            yield return GetBlock(store, blockHash);
        }
    }

    private static Transaction GetTransaction(Libplanet.Store.Store store, TxId txId)
    {
        if (!(store.GetTransaction(txId) is { } tx))
        {
            throw Utils.Error($"cannot find the tx with the tx id[{txId.ToString()}]");
        }

        return tx;
    }

    private static IEnumerable<int> BuildTxIdBlockHashIndex(Libplanet.Store.Store store, int offset, int limit)
    {
        if (!(store.GetCanonicalChainId() is { } chainId))
        {
            throw Utils.Error("Cannot find the main branch of the blockchain.");
        }

        var index = offset;
        foreach (BlockHash blockHash in store.IterateIndexes(chainId, offset, limit))
        {
            yield return index++;
            if (!(store.GetBlockDigest(blockHash) is { } blockDigest))
            {
                throw Utils.Error(
                    $"Block is missing for BlockHash: {blockHash} index: {index}.");
            }

            foreach (TxId txId in blockDigest.TxIds)
            {
                store.PutTxIdBlockHashIndex(txId, blockHash);
            }
        }
    }
}
