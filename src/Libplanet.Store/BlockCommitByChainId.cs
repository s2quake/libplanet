using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockCommitByChainId(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<Guid, BlockCommit>(dictionary)
{
    protected override byte[] GetBytes(BlockCommit value) => ModelSerializer.SerializeToBytes(value);

    protected override Guid GetKey(KeyBytes keyBytes) => new(keyBytes.AsSpan());

    protected override KeyBytes GetKeyBytes(Guid key) => new(key.ToByteArray());

    protected override BlockCommit GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockCommit>(bytes);
}
