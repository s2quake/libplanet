using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

internal class GetBlockHashesMsg : MessageContent
{
    public GetBlockHashesMsg(BlockHash locator)
    {
        Locator = locator;
    }

    public GetBlockHashesMsg(byte[][] dataFrames)
    {
        Locator = new BlockHash(dataFrames[0]);
    }

    public BlockHash Locator { get; }

    public override MessageType Type => MessageType.GetBlockHashes;

    public override IEnumerable<byte[]> DataFrames
    {
        get
        {
            var frames = new List<byte[]>
            {
                Locator.Bytes.ToArray(),
            };
            return frames;
        }
    }
}
