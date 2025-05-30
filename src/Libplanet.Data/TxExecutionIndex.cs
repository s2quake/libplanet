using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class TxExecutionIndex(IDatabase database)
    : KeyedIndexBase<TxId, TxExecution>(database.GetOrAdd("tx_execution"))
{
    protected override byte[] ValueToBytes(TxExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override TxExecution BytesToValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<TxExecution>(bytes);

    protected override string KeyToString(TxId key) => key.ToString();

    protected override TxId StringToKey(string key)=> TxId.Parse(key);
}
