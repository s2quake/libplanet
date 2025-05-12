using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class TransactionCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<TxId, Transaction>(dictionary)
{
    public void Add(Block block)
    {
        foreach (var transaction in block.Transactions)
        {
            Add(transaction.Id, transaction);
        }
    }

    public void RemoveRange(IEnumerable<TxId> txIds)
    {
        foreach (var txId in txIds)
        {
            Remove(txId);
        }
    }

    protected override byte[] GetBytes(Transaction value) => ModelSerializer.SerializeToBytes(value);

    protected override TxId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(TxId key) => new(key.Bytes);

    protected override Transaction GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<Transaction>(bytes);
}
