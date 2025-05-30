using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class BlockExecutionIndex(IDatabase database)
    : KeyedIndexBase<BlockHash, BlockExecution>(database.GetOrAdd("block_execution"))
{
    protected override byte[] ValueToBytes(BlockExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockExecution BytesToValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<BlockExecution>(bytes);

    protected override string KeyToString(BlockHash key) => key.ToString();

    protected override BlockHash StringToKey(string key) => BlockHash.Parse(key);
}
