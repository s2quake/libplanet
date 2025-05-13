using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockHashStore(Guid chainId, IDatabase database)
    : CollectionBase<int, BlockHash>(database.GetOrAdd($"{chainId}_block_hash"))
{
    public BlockHash this[Index index]
    {
        get
        {
            if (index.IsFromEnd)
            {
                return this[Count - index.Value];
            }

            if (index.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return this[index.Value];
        }
    }

    public void Add(Block block) => Add(block.Height, block.BlockHash);

    public IEnumerable<BlockHash> IterateIndexes(int offset = 0, int? limit = null)
    {
        var end = checked(limit is { } l ? offset + l : int.MaxValue);
        for (var i = offset; i < end; i++)
        {
            if (TryGetValue(i, out var blockHash))
            {
                yield return blockHash;
            }
            else
            {
                break;
            }
        }
    }

    protected override byte[] GetBytes(BlockHash value) => [.. value.Bytes];

    protected override int GetKey(KeyBytes keyBytes) => BitConverter.ToInt32([.. keyBytes.Bytes]);

    protected override KeyBytes GetKeyBytes(int key) => new(BitConverter.GetBytes(key));

    protected override BlockHash GetValue(byte[] bytes) => new(bytes);
}
