using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockDigestStore(IDatabase database)
    : CollectionBase<BlockHash, BlockDigest>(database.GetOrAdd("block_digest"))
{
    public void Add(Block block) => Add((BlockDigest)block);

    protected override byte[] GetBytes(BlockDigest value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockHash GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(BlockHash key) => new(key.Bytes);

    protected override BlockDigest GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockDigest>(bytes);
}
