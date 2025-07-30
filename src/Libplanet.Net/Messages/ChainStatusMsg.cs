using Destructurama.Attributed;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages
{
    internal class ChainStatusMsg : MessageContent, IBlockExcerpt
    {
        public ChainStatusMsg(
            int protocolVersion,
            BlockHash genesisHash,
            long tipIndex,
            BlockHash tipHash)
        {
            ProtocolVersion = protocolVersion;
            GenesisHash = genesisHash;
            TipIndex = tipIndex;
            TipHash = tipHash;
        }

        public ChainStatusMsg(byte[][] dataFrames)
        {
            ProtocolVersion = BitConverter.ToInt32(dataFrames[0], 0);
            GenesisHash = new BlockHash(dataFrames[1]);
            TipIndex = BitConverter.ToInt64(dataFrames[2], 0);
            TipHash = new BlockHash(dataFrames[3]);
        }

        public int ProtocolVersion { get; }

        [LogAsScalar]
        public BlockHash GenesisHash { get; }

        public long TipIndex { get; }

        [LogAsScalar]
        public BlockHash TipHash { get; }

        long IBlockExcerpt.Index => TipIndex;

        [LogAsScalar]
        BlockHash IBlockExcerpt.Hash => TipHash;

        public override MessageType Type => MessageType.ChainStatus;

        public override IEnumerable<byte[]> DataFrames => new[]
        {
            BitConverter.GetBytes(ProtocolVersion),
            GenesisHash.ToByteArray(),
            BitConverter.GetBytes(TipIndex),
            TipHash.ToByteArray(),
        };
    }
}
