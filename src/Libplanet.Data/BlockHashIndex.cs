using Libplanet.Types;

namespace Libplanet.Data;

public sealed class BlockHashIndex(IDatabase database, int cacheSize = 100)
    : IndexBase<int, BlockHash>(database.GetOrAdd("block_hash"), cacheSize)
{
    public int Height { get; set; } = -1;

    public BlockHash this[Index index]
    {
        get => base[index.GetOffset(Height + 1)];
        set => base[index.GetOffset(Height + 1)] = value;
    }

    public IEnumerable<BlockHash> this[Range range]
    {
        get
        {
            var (offset, length) = range.GetOffsetAndLength(Height + 1);
            var start = offset;
            var end = offset + length;

            for (var i = start; i < end; i++)
            {
                if (TryGetValue(i, out var blockHash))
                {
                    yield return blockHash;
                }
            }
        }
    }

    public void Add(Block block) => Add(block.Height, block.BlockHash);

    protected override string KeyToString(int key) => key.ToString("R");

    protected override int StringToKey(string key) => int.Parse(key);

    protected override byte[] ValueToBytes(BlockHash value) => [.. value.Bytes];

    protected override BlockHash BytesToValue(byte[] bytes) => new(bytes);
}
