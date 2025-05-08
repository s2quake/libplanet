using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public sealed class BlockHashCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<TxId, ImmutableArray<BlockHash>>(dictionary)
{
    protected override byte[] GetBytes(ImmutableArray<BlockHash> value) => ModelSerializer.SerializeToBytes(value);

    protected override TxId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(TxId key) => new(key.Bytes);

    protected override ImmutableArray<BlockHash> GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<ImmutableArray<BlockHash>>(bytes);
}
