using System.Diagnostics.CodeAnalysis;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class TxExecutionStore(IDatabase database)
    : CollectionBase<TxId, ImmutableArray<TxExecution>>(database.GetOrAdd("tx_execution"))
{
    public void Add(TxExecution txExecution)
    {
        if (!TryGetValue(txExecution.TxId, out var txExecutions))
        {
            txExecutions = [];
        }

        this[txExecution.TxId] = txExecutions.Add(txExecution);
    }

    public void AddRange(IEnumerable<TxExecution> txExecutions)
    {
        foreach (var txExecution in txExecutions)
        {
            Add(txExecution);
        }
    }

    public bool Contains(TxId txId, BlockHash blockHash)
    {
        if (TryGetValue(txId, out var txExecutions))
        {
            return txExecutions.Any(txExecution => txExecution.BlockHash == blockHash);
        }

        return false;
    }

    public TxExecution this[TxId txId, BlockHash blockHash]
    {
        get
        {
            if (TryGetValue(txId, out var txExecutions))
            {
                return txExecutions.First(txExecution => txExecution.BlockHash == blockHash);
            }

            throw new KeyNotFoundException($"No such key: ${txId}.");
        }
    }

    public bool TryGetValue(TxId txId, BlockHash blockHash, [MaybeNullWhen(false)] out TxExecution txExecution)
    {
        if (TryGetValue(txId, out var txExecutions))
        {
            txExecution = txExecutions.FirstOrDefault(txExecution => txExecution.BlockHash == blockHash);
            return txExecution != null;
        }

        txExecution = null;
        return false;
    }

    public TxExecution? GetValueOrDefault(TxId txId, BlockHash blockHash)
    {
        if (TryGetValue(txId, out var txExecutions))
        {
            return txExecutions.FirstOrDefault(txExecution => txExecution.BlockHash == blockHash);
        }

        return null;
    }

    protected override byte[] GetBytes(ImmutableArray<TxExecution> value) => ModelSerializer.SerializeToBytes(value);

    protected override TxId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(TxId key) => new(key.Bytes);

    protected override ImmutableArray<TxExecution> GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<ImmutableArray<TxExecution>>(bytes);
}
