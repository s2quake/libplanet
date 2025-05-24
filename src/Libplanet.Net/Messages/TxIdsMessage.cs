using Libplanet.Serialization;
using Libplanet.Types.Transactions;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class TxIdsMessage : MessageContent
{
    // public TxIdsMessage(IEnumerable<TxId> txIds)
    // {
    //     Ids = txIds;
    // }

    // public TxIdsMessage(byte[][] dataFrames)
    // {
    //     int txCount = BitConverter.ToInt32(dataFrames[0], 0);
    //     Ids = dataFrames
    //         .Skip(1).Take(txCount)
    //         .Select(ba => new TxId(ba))
    //         .ToList();
    // }

    [Property(0)]
    public ImmutableArray<TxId> Ids { get; init; } = [];

    public override MessageType Type => MessageType.TxIds;

    // public override IEnumerable<byte[]> DataFrames
    // {
    //     get
    //     {
    //         var frames = new List<byte[]>();
    //         frames.Add(BitConverter.GetBytes(Ids.Count()));
    //         frames.AddRange(Ids.Select(id => id.Bytes.ToArray()));
    //         return frames;
    //     }
    // }
}
