using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public abstract class TransactionIndexBase(IDatabase database, string name, int cacheSize = 100)
    : KeyedIndexBase<TxId, Transaction>(database.GetOrAdd(name), cacheSize)
{
    protected override byte[] ValueToBytes(Transaction value) => ModelSerializer.SerializeToBytes(value);

    protected override Transaction BytesToValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<Transaction>(bytes);

    protected override string KeyToString(TxId key) => key.ToString();

    protected override TxId StringToKey(string key) => TxId.Parse(key);
}
