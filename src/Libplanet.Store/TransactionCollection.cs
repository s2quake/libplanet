using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class TransactionCollection(IDictionary<KeyBytes, byte[]> dictionary)
{
    public Transaction this[TxId txid]
    {
        get
        {
            var key = new KeyBytes(txid.Bytes);
            var bytes = dictionary[key];
            return ModelSerializer.DeserializeFromBytes<Transaction>(bytes);
        }
    }

    public void Add(Transaction tx)
    {
        var key = new KeyBytes(tx.Id.Bytes);
        dictionary.Add(key, ModelSerializer.SerializeToBytes(tx));
    }

}
