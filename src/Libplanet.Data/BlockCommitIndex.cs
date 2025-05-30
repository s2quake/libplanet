using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class BlockCommitIndex(IDatabase database)
    : KeyedIndexBase<BlockHash, BlockCommit>(database.GetOrAdd("block_commit"))
{
    protected override byte[] ValueToBytes(BlockCommit value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockCommit BytesToValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockCommit>(bytes);

    protected override string KeyToString(BlockHash key) => key.ToString();

    protected override BlockHash StringToKey(string key) => BlockHash.Parse(key);
}
