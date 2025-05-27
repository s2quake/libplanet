using Libplanet.Serialization;
using Libplanet.Types.Transactions;

namespace Libplanet.Data;

public sealed class TxExecutionIndex(IDatabase database)
    : IndexBase<TxId, TxExecution>(database.GetOrAdd("tx_execution"))
{
    protected override byte[] GetBytes(TxExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override TxExecution GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<TxExecution>(bytes);
}
