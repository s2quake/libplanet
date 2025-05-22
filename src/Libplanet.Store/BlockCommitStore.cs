using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockCommitStore(IDatabase database)
    : StoreBase<BlockHash, BlockCommit>(database.GetOrAdd("block_commit"))
{
    protected override byte[] GetBytes(BlockCommit value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockCommit GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockCommit>(bytes);
}
