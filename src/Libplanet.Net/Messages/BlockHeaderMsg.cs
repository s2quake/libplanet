using Bencodex.Types;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

internal class BlockHeaderMsg : MessageContent
{
    private static readonly Codec Codec = new Codec();

    public BlockHeaderMsg(BlockHash genesisHash, BlockExcerpt header)
    {
        GenesisHash = genesisHash;
        HeaderDictionary = ModelSerializer.Serialize(header);
    }

    public BlockHeaderMsg(byte[][] dataFrames)
    {
        GenesisHash = new BlockHash(dataFrames[0]);
        HeaderDictionary = Codec.Decode(dataFrames[1]);
    }

    public BlockHash GenesisHash { get; }

    public IValue HeaderDictionary { get; }

    public long HeaderIndex => ModelSerializer.Deserialize<BlockHeader>(HeaderDictionary).Height;

    public BlockExcerpt HeaderHash
        => ModelSerializer.Deserialize<BlockExcerpt>(HeaderDictionary);

    public override MessageType Type => MessageType.BlockHeaderMessage;

    public override IEnumerable<byte[]> DataFrames => new[]
    {
        GenesisHash.Bytes.ToArray(),
        Codec.Encode(HeaderDictionary),
    };

    public BlockExcerpt GetHeader()
    {
        return ModelSerializer.Deserialize<BlockExcerpt>(HeaderDictionary);
    }
}
