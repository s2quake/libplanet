using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

internal sealed class BlockCommitByBlockHash(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<BlockHash, BlockCommit>(dictionary)
{
    protected override byte[] GetBytes(BlockCommit value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockHash GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(BlockHash key) => new(key.Bytes);

    protected override BlockCommit GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockCommit>(bytes);
}
