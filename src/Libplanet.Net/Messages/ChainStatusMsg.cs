using Destructurama.Attributed;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages
{
    internal class ChainStatusMsg : MessageContent
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

        public override MessageType Type => MessageType.ChainStatus;

        public override IEnumerable<byte[]> DataFrames => new[]
        {
            BitConverter.GetBytes(ProtocolVersion),
            GenesisHash.Bytes.ToArray(),
            BitConverter.GetBytes(TipIndex),
            TipHash.Bytes.ToArray(),
        };

        public static implicit operator BlockExcerpt(ChainStatusMsg msg)
            => new BlockExcerpt
            {
                Index = msg.TipIndex,
                ProtocolVersion = msg.ProtocolVersion,
                Hash = msg.TipHash,
            };
    }
}
