using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

internal class BlockHeaderMsg : MessageContent
{
    public BlockHeaderMsg(BlockHash genesisHash, BlockExcerpt header)
    {
        GenesisHash = genesisHash;
        HeaderDictionary = ModelSerializer.SerializeToBytes(header);
    }

    public BlockHeaderMsg(byte[][] dataFrames)
    {
        GenesisHash = new BlockHash(dataFrames[0]);
        HeaderDictionary = dataFrames[1];
    }

    public BlockHash GenesisHash { get; }

    public byte[] HeaderDictionary { get; }

    public long HeaderIndex => ModelSerializer.DeserializeFromBytes<BlockHeader>(HeaderDictionary).Height;

    public BlockExcerpt HeaderHash
        => ModelSerializer.DeserializeFromBytes<BlockExcerpt>(HeaderDictionary);

    public override MessageType Type => MessageType.BlockHeaderMessage;

    public override IEnumerable<byte[]> DataFrames => new[]
    {
        GenesisHash.Bytes.ToArray(),
        HeaderDictionary,
    };

    public BlockExcerpt GetHeader()
    {
        return ModelSerializer.DeserializeFromBytes<BlockExcerpt>(HeaderDictionary);
    }
}
