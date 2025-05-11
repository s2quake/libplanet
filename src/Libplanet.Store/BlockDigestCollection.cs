using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockDigestCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<BlockHash, BlockDigest>(dictionary)
{
    public void Add(Block block) => Add(block.BlockHash, (BlockDigest)block);

    protected override byte[] GetBytes(BlockDigest value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockHash GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(BlockHash key) => new(key.Bytes);

    protected override BlockDigest GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockDigest>(bytes);
}
