using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockHashByHeight(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<int, BlockHash>(dictionary)
{
    protected override byte[] GetBytes(BlockHash value) => ModelSerializer.SerializeToBytes(value);

    protected override int GetKey(KeyBytes keyBytes) => BitConverter.ToInt32(keyBytes.Bytes.AsSpan());

    protected override KeyBytes GetKeyBytes(int key) => new(BitConverter.GetBytes(key));

    protected override BlockHash GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<BlockHash>(bytes);
}
