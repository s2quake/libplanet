using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

internal sealed record class BlockHashesMessage : MessageContent
{
    public BlockHashesMessage(ImmutableArray<BlockHash> hashes)
    {
        Hashes = hashes;
    }

    // public BlockHashesMessage(byte[][] dataFrames)
    // {
    //     int hashCount = BitConverter.ToInt32(dataFrames[0], 0);
    //     var hashes = new List<BlockHash>(hashCount);
    //     if (hashCount > 0)
    //     {
    //         for (int i = 1, end = hashCount + 1; i < end; i++)
    //         {
    //             hashes.Add(new BlockHash(dataFrames[i]));
    //         }
    //     }

    //     Hashes = hashes;
    // }

    public override MessageType Type => MessageType.BlockHashes;

    [Property(0)]
    public ImmutableArray<BlockHash> Hashes { get; }


    // public override IEnumerable<byte[]> DataFrames
    // {
    //     get
    //     {
    //         var frames = new List<byte[]>();
    //         frames.Add(BitConverter.GetBytes(Hashes.Count()));
    //         frames.AddRange(Hashes.Select(hash => hash.Bytes.ToArray()));
    //         return frames;
    //     }
    // }
}
