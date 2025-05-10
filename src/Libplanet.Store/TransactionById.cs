using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

internal sealed class TransactionById(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<TxId, Transaction>(dictionary)
{
    protected override byte[] GetBytes(Transaction value) => ModelSerializer.SerializeToBytes(value);

    protected override TxId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(TxId key) => new(key.Bytes);

    protected override Transaction GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<Transaction>(bytes);
}
