using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class BlockCommitIndex(IDatabase database, int cacheSize = 100)
    : KeyedIndexBase<BlockHash, BlockCommit>(database.GetOrAdd("block_commit"), cacheSize)
{
    public void Prune(int height)
    {
        foreach (var (key, value) in this.ToArray())
        {
            if (value.Height < height)
            {
                Remove(key);
            }
        }
    }

    protected override byte[] ValueToBytes(BlockCommit value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockCommit BytesToValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockCommit>(bytes);

    protected override string KeyToString(BlockHash key) => key.ToString();

    protected override BlockHash StringToKey(string key) => BlockHash.Parse(key);
}
