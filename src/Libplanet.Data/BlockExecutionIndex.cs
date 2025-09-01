using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class BlockExecutionIndex(IDatabase database, int cacheSize = 100)
    : KeyedIndexBase<BlockHash, BlockExecutionInfo>(database.GetOrAdd("block_execution"), cacheSize)
{
    protected override byte[] ValueToBytes(BlockExecutionInfo value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockExecutionInfo BytesToValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<BlockExecutionInfo>(bytes);

    protected override string KeyToString(BlockHash key) => key.ToString();

    protected override BlockHash StringToKey(string key) => BlockHash.Parse(key);
}
