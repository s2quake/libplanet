using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockHashByHeight(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<long, BlockHash>(dictionary)
{
    protected override byte[] GetBytes(BlockHash value) => ModelSerializer.SerializeToBytes(value);

    protected override long GetKey(KeyBytes keyBytes) => BitConverter.ToInt64(keyBytes.Bytes.AsSpan());

    protected override KeyBytes GetKeyBytes(long key) => new(BitConverter.GetBytes(key));

    protected override BlockHash GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<BlockHash>(bytes);
}
