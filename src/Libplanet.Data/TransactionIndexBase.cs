using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public abstract class TransactionIndexBase(IDatabase database, string name)
    : IndexBase<TxId, Transaction>(database.GetOrAdd(name))
{
    protected override byte[] GetBytes(Transaction value) => ModelSerializer.SerializeToBytes(value);

    protected override Transaction GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<Transaction>(bytes);
}
