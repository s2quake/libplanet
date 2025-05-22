using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockExecutionStore(IDatabase database)
    : StoreBase<BlockHash, BlockExecution>(database.GetOrAdd("block_execution"))
{
    protected override byte[] GetBytes(BlockExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockExecution GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<BlockExecution>(bytes);
}
