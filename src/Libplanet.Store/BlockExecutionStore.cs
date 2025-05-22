using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class BlockExecutionStore(IDatabase database)
    : StoreBase<BlockHash, BlockExecution>(database.GetOrAdd("block_execution"))
{
    protected override byte[] GetBytes(BlockExecution value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockHash GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(BlockHash key) => new(key.Bytes);

    protected override BlockExecution GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<BlockExecution>(bytes);
}
