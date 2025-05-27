using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Data;

public sealed class BlockCommitIndex(IDatabase database)
    : IndexBase<BlockHash, BlockCommit>(database.GetOrAdd("block_commit"))
{
    protected override byte[] GetBytes(BlockCommit value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockCommit GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockCommit>(bytes);
}
