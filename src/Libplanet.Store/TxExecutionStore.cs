using Libplanet.Serialization;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class TxExecutionStore(IDatabase database)
    : StoreBase<TxId, TxExecution>(database.GetOrAdd("tx_execution"))
{
    protected override byte[] GetBytes(TxExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override TxExecution GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<TxExecution>(bytes);
}
