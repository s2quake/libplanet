using Libplanet.Serialization;
using Libplanet.Types.Transactions;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class GetTransactionMessage : MessageContent, IEquatable<GetTransactionMessage>
{
    // public GetTransactionMessage(params TxId[] txIds)
    // {
    //     TxIds = txIds.ToImmutableArray();
    // }

    // 
    // public GetTransactionMessage(IEnumerable<TxId> txIds)
    // {
    //     TxIds = txIds;
    // }

    // public GetTransactionMessage(byte[][] dataFrames)
    // {
    //     int txCount = BitConverter.ToInt32(dataFrames[0], 0);
    //     TxIds = dataFrames
    //         .Skip(1).Take(txCount)
    //         .Select(ba => new TxId(ba))
    //         .ToList();
    // }

    [Property(0)]
    public ImmutableArray<TxId> TxIds { get; }

    public override MessageType Type => MessageType.GetTxs;

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
    
    public bool Equals(GetTransactionMessage? other) => ModelResolver.Equals(this, other);

    // public override IEnumerable<byte[]> DataFrames
    // {
    //     get
    //     {
    //         var frames = new List<byte[]>();
    //         frames.Add(BitConverter.GetBytes(TxIds.Count()));
    //         frames.AddRange(TxIds.Select(id => id.Bytes.ToArray()));
    //         return frames;
    //     }
    // }
}
