using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class BlockHashesByTxId(IDatabase database)
    : CollectionBase<TxId, ImmutableSortedSet<BlockHash>>(database.GetOrAdd("block_hashes_by_tx_id"))
{
    public void Add(TxId txId, BlockHash blockHash)
    {
        if (TryGetValue(txId, out ImmutableSortedSet<BlockHash>? blockHashes))
        {
            blockHashes = blockHashes.Add(blockHash);
        }
        else
        {
            blockHashes = [blockHash];
        }

        Add(txId, blockHashes);
    }

    public void Remove(TxId txId, BlockHash blockHash)
    {
        if (TryGetValue(txId, out ImmutableSortedSet<BlockHash>? blockHashes))
        {
            blockHashes = blockHashes.Remove(blockHash);
            if (blockHashes.IsEmpty)
            {
                Remove(txId);
            }
            else
            {
                Add(txId, blockHashes);
            }
        }
    }

    protected override byte[] GetBytes(ImmutableSortedSet<BlockHash> value)
        => ModelSerializer.SerializeToBytes(value.ToArray());

    protected override TxId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(TxId key) => new(key.Bytes);

    protected override ImmutableSortedSet<BlockHash> GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<ImmutableSortedSet<BlockHash>>(bytes);
}
