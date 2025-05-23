using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class BlockHeaderMessage : MessageContent
{
    // public BlockHeaderMessage(BlockHash genesisHash, BlockExcerpt header)
    // {
    //     GenesisHash = genesisHash;
    //     HeaderDictionary = ModelSerializer.SerializeToBytes(header);
    // }

    // public BlockHeaderMessage(byte[][] dataFrames)
    // {
    //     GenesisHash = new BlockHash(dataFrames[0]);
    //     HeaderDictionary = dataFrames[1];
    // }

    public required BlockHash GenesisHash { get; init; }

    public required BlockExcerpt Excerpt { get; init; }

    // public byte[] HeaderDictionary { get; }

    // public long HeaderIndex => ModelSerializer.DeserializeFromBytes<BlockHeader>(HeaderDictionary).Height;

    // public BlockExcerpt HeaderHash
    //     => ModelSerializer.DeserializeFromBytes<BlockExcerpt>(HeaderDictionary);

    public override MessageType Type => MessageType.BlockHeaderMessage;

    // public override IEnumerable<byte[]> DataFrames => new[]
    // {
    //     GenesisHash.Bytes.ToArray(),
    //     HeaderDictionary,
    // };

    // public BlockExcerpt GetHeader()
    // {
    //     return ModelSerializer.DeserializeFromBytes<BlockExcerpt>(HeaderDictionary);
    // }
}
