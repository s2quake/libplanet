using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Data;

public sealed class BlockDigestIndex(IDatabase database)
    : IndexBase<BlockHash, BlockDigest>(database.GetOrAdd("block_digest"))
{
    public void Add(Block block) => Add((BlockDigest)block);

    protected override byte[] GetBytes(BlockDigest value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockDigest GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockDigest>(bytes);
}
