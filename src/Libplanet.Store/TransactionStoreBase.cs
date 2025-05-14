using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public abstract class TransactionStoreBase(IDatabase database, string name)
    : CollectionBase<TxId, Transaction>(database.GetOrAdd(name))
{
    protected override byte[] GetBytes(Transaction value) => ModelSerializer.SerializeToBytes(value);

    protected override TxId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(TxId key) => new(key.Bytes);

    protected override Transaction GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<Transaction>(bytes);
}
