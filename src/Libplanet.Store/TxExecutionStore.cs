using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class TxExecutionStore(IDatabase database)
    : CollectionBase<(BlockHash BlockHash, TxId TxId), TxExecution>(database.GetOrAdd("tx_execution"))
{
    public TxExecution this[BlockHash blockHash, TxId txId]
    {
        get => this[(blockHash, txId)];
        set => this[(blockHash, txId)] = value;
    }

    public void Add(TxExecution txExecution) => Add((txExecution.BlockHash, txExecution.TxId), txExecution);

    public void AddRange(IEnumerable<TxExecution> txExecutions)
    {
        foreach (var txExecution in txExecutions)
        {
            Add(txExecution);
        }
    }

    protected override byte[] GetBytes(TxExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override (BlockHash BlockHash, TxId TxId) GetKey(KeyBytes keyBytes)
        => (new BlockHash(keyBytes.Bytes[..BlockHash.Size]), new TxId(keyBytes.Bytes[BlockHash.Size..]));

    protected override KeyBytes GetKeyBytes((BlockHash BlockHash, TxId TxId) key)
        => new(key.BlockHash.Bytes.AddRange(key.TxId.Bytes));

    protected override TxExecution GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<TxExecution>(bytes);
}
