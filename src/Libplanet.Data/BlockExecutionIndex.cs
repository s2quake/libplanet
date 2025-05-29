using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class BlockExecutionIndex(IDatabase database)
    : KeyedIndexBase<BlockHash, BlockExecution>(database.GetOrAdd("block_execution"))
{
    protected override byte[] GetBytes(BlockExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockExecution GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<BlockExecution>(bytes);
}
