using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class TxExecutionIndex(IDatabase database, int cacheSize = 100)
    : KeyedIndexBase<TxId, TransactionExecutionInfo>(database.GetOrAdd("tx_execution"), cacheSize)
{
    protected override byte[] ValueToBytes(TransactionExecutionInfo value) => ModelSerializer.SerializeToBytes(value);

    protected override TransactionExecutionInfo BytesToValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<TransactionExecutionInfo>(bytes);

    protected override string KeyToString(TxId key) => key.ToString();

    protected override TxId StringToKey(string key)=> TxId.Parse(key);
}
