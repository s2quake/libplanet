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
        Guid? canon = store.ChainId;
        var headerWithoutHash = ("Chain ID", "Height", "Canon?");
        var headerWithHash = ("Chain ID", "Height", "Canon?", "Hash");
        var chainIds = store.Chains.Keys.Select(id =>
        {
            var chain = store.Chains[id];
            var height = chain.Height - 1;
            return (
                id.ToString(),
                height.ToString(CultureInfo.InvariantCulture),
                id == canon ? "*" : string.Empty,
                store.BlockDigests[chain.BlockHashes[height]].BlockHash.ToString());
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
        var blockHashes = store.BlockHashesByTxId[new TxId(ByteUtility.ParseHex(strTxId))];
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
        if (!(store.BlockHashesByTxId[txId] is { }))
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
        var store = new Libplanet.Store.Store(new MemoryDatabase());
        var chain = store.Chains[store.ChainId];
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
        var chain = store.Chains.GetOrAdd(store.ChainId);
        if (!(chain.Blocks[blockHash] is { } block))
        {
            throw Utils.Error($"cannot find the block with the hash[{blockHash.ToString()}]");
        }

        return block;
    }

    private static BlockHash GetBlockHash(Libplanet.Store.Store store, int blockHeight)
    {
        if (store.ChainId == Guid.Empty)
        {
            throw Utils.Error("Cannot find the main branch of the blockchain.");
        }

        var chain = store.GetOrAdd(store.ChainId);
        if (!chain.BlockHashes.TryGetValue(blockHeight, out var blockHash))
        {
            throw Utils.Error(
                $"Cannot find the block with the height {blockHeight}" +
                $" within the blockchain {store.ChainId}.");
        }

        return blockHash;
    }

    private static IEnumerable<Block> IterateBlocks(Libplanet.Store.Store store, TxId txId)
    {
        foreach (var blockHash in store.BlockHashesByTxId[txId])
        {
            yield return GetBlock(store, blockHash);
        }
    }

    private static Transaction GetTransaction(Libplanet.Store.Store store, TxId txId)
    {
        if (!store.Transactions.TryGetValue(txId, out var tx))
        {
            throw Utils.Error($"cannot find the tx with the tx id[{txId.ToString()}]");
        }

        return tx;
    }

    private static IEnumerable<int> BuildTxIdBlockHashIndex(Libplanet.Store.Store store, int offset, int limit)
    {
        if (store.ChainId == Guid.Empty)
        {
            throw Utils.Error("Cannot find the main branch of the blockchain.");
        }

        var index = offset;
        var chain = store.GetOrAdd(store.ChainId);
        foreach (BlockHash blockHash in chain.BlockHashes.IterateIndexes(offset, limit))
        {
            yield return index++;
            if (!store.BlockDigests.TryGetValue(blockHash, out var blockDigest))
            {
                throw Utils.Error(
                    $"Block is missing for BlockHash: {blockHash} index: {index}.");
            }

            foreach (TxId txId in blockDigest.TxIds)
            {
                store.BlockHashesByTxId.Add(txId, blockHash);
            }
        }
    }
}
