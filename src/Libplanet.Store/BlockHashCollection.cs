using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockHashCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<int, BlockHash>(dictionary)
{
    public void Add(Block block) => Add(block.Height, block.BlockHash);

    protected override byte[] GetBytes(BlockHash value) => [.. value.Bytes];

    protected override int GetKey(KeyBytes keyBytes) => BitConverter.ToInt32([.. keyBytes.Bytes]);

    protected override KeyBytes GetKeyBytes(int key) => new(BitConverter.GetBytes(key));

    protected override BlockHash GetValue(byte[] bytes) => new(bytes);
}
