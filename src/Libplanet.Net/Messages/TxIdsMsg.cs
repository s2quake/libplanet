using Libplanet.Types.Tx;

namespace Libplanet.Net.Messages
{
    internal class TxIdsMsg : MessageContent
    {
        public TxIdsMsg(IEnumerable<TxId> txIds)
        {
            Ids = txIds;
        }

        public TxIdsMsg(byte[][] dataFrames)
        {
            int txCount = BitConverter.ToInt32(dataFrames[0], 0);
            Ids = dataFrames
                .Skip(1).Take(txCount)
                .Select(ba => new TxId(ba))
                .ToList();
        }

        public IEnumerable<TxId> Ids { get; }

        public override MessageType Type => MessageType.TxIds;

        public override IEnumerable<byte[]> DataFrames
        {
            get
            {
                var frames = new List<byte[]>();
                frames.Add(BitConverter.GetBytes(Ids.Count()));
                frames.AddRange(Ids.Select(id => id.ByteArray.ToArray()));
                return frames;
            }
        }
    }
}
