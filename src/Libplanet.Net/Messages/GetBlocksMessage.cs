using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class GetBlocksMessage : MessageContent
{
    // public GetBlocksMsg(IEnumerable<BlockHash> hashes, int chunkSize = 100)
    // {
    //     if (chunkSize <= 0)
    //     {
    //         throw new ArgumentOutOfRangeException(
    //             nameof(chunkSize),
    //             "Chunk size must be greater than 0.");
    //     }

    //     BlockHashes = hashes;
    //     ChunkSize = chunkSize;
    // }

    // public GetBlocksMsg(byte[][] dataFrames)
    // {
    //     int hashCount = BitConverter.ToInt32(dataFrames[0], 0);
    //     BlockHashes = dataFrames
    //         .Skip(1).Take(hashCount)
    //         .Select(ba => new BlockHash(ba))
    //         .ToList();
    //     ChunkSize = BitConverter.ToInt32(dataFrames[1 + hashCount], 0);
    // }

    [Property(0)]
    public ImmutableArray<BlockHash> BlockHashes { get; init; }

    [Property(1)]
    public int ChunkSize { get; init; }

    public override MessageType Type => MessageType.GetBlocks;

    // public override IEnumerable<byte[]> DataFrames
    // {
    //     get
    //     {
    //         var frames = new List<byte[]>();
    //         frames.Add(BitConverter.GetBytes(BlockHashes.Count()));
    //         frames.AddRange(BlockHashes.Select(hash => hash.Bytes.ToArray()));
    //         frames.Add(BitConverter.GetBytes(ChunkSize));
    //         return frames;
    //     }
    // }
}
