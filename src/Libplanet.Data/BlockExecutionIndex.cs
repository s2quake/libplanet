using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class BlockExecutionIndex(IDatabase database)
    : IndexBase<BlockHash, BlockExecution>(database.GetOrAdd("block_execution"))
{
    protected override byte[] GetBytes(BlockExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockExecution GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<BlockExecution>(bytes);
}
