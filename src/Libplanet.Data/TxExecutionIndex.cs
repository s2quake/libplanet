using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class TxExecutionIndex(IDatabase database, int cacheSize = 100)
    : KeyedIndexBase<TxId, TxExecutionInfo>(database.GetOrAdd("tx_execution"), cacheSize)
{
    protected override byte[] ValueToBytes(TxExecutionInfo value) => ModelSerializer.SerializeToBytes(value);

    protected override TxExecutionInfo BytesToValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<TxExecutionInfo>(bytes);

    protected override string KeyToString(TxId key) => key.ToString();

    protected override TxId StringToKey(string key)=> TxId.Parse(key);
}
