using Libplanet.Serialization;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public abstract class TransactionStoreBase(IDatabase database, string name)
    : StoreBase<TxId, Transaction>(database.GetOrAdd(name))
{
    protected override byte[] GetBytes(Transaction value) => ModelSerializer.SerializeToBytes(value);

    protected override Transaction GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<Transaction>(bytes);
}
