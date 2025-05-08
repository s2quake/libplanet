using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<BlockHash, Block>(dictionary)
{
    public BlockDigest GetBlockDigest(BlockHash blockHash) => BlockDigest.Create(this[blockHash]);

    protected override byte[] GetBytes(Block value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockHash GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(BlockHash key) => new(key.Bytes);

    protected override Block GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<Block>(bytes);
}
