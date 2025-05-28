using global::Cocona;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet.Extensions.Cocona.Commands;

public class StatsCommand
{
    private const string StoreArgumentDescription =
        "The URI denotes the type and path of concrete class for " + nameof(Libplanet.Data.Repository) + "."
        + "<store-type>://<store-path> (e.g., rocksdb+file:///path/to/store)";

    [Command(Description = "Outputs a summary of a stored chain in a CSV format.")]
    public void Summary(
        [Option('p', Description = StoreArgumentDescription)]
        string path,
        [Option('r', Description = "Whether to include header row")]
        bool header,
        [Option('o', Description =
            "Starting index offset; " +
            "supports negative indexing")]
        int offset = 0,
        [Option('l', Description =
            "Maximum number of results to return; " +
            "no limit by default")]
        int? limit = null) => Summary(
            repository: Utils.LoadStoreFromUri(path),
            header: header,
            offset: offset,
            limit: limit);

    internal void Summary(
        Repository repository,
        bool header,
        int offset,
        int? limit)
    {
        if (limit is { } && limit < 1)
        {
            throw new ArgumentException($"limit must be at least 1: {limit}");
        }

        long chainLength = repository.GenesisHeight;

        if (offset >= chainLength || (offset < 0 && chainLength + offset < 0))
        {
            throw new ArgumentException(
                $"invalid offset value {offset} for found chain length {chainLength}");
        }

        var chain = repository;
        var end = limit is { } l ? new Index(offset + l) : new Index(0, true);
        var range = new Range(new Index(offset, false), end);
        IEnumerable<BlockHash> hashes = chain.BlockHashes[range];

        if (header)
        {
            Console.WriteLine("index,hash,difficulty,miner,txCount,timestamp,perceivedTime");
        }

        foreach (var hash in hashes)
        {
            BlockDigest blockDigest = repository.BlockDigests[hash];
            BlockHeader blockHeader = blockDigest.Header;

            Console.WriteLine(
                $"{blockHeader.Height}," +
                $"{blockHeader.Proposer}," +
                $"{blockDigest.TxIds}," +
                $"{blockHeader.Timestamp.ToUnixTimeMilliseconds()}");
        }
    }
}
