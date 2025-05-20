using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class TxExecutionStore(IDatabase database)
    : StoreBase<TxId, TxExecution>(database.GetOrAdd("tx_execution"))
{
    protected override byte[] GetBytes(TxExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override TxId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(TxId key) => new(key.Bytes);

    protected override TxExecution GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<TxExecution>(bytes);
}
