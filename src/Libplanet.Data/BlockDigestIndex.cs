using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class BlockDigestIndex(IDatabase database, int cacheSize = 100)
    : KeyedIndexBase<BlockHash, BlockDigest>(database.GetOrAdd("block_digest"), cacheSize)
{
    public void Add(Block block) => Add((BlockDigest)block);

    protected override byte[] ValueToBytes(BlockDigest value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockDigest BytesToValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<BlockDigest>(bytes);

    protected override string KeyToString(BlockHash key) => key.ToString();

    protected override BlockHash StringToKey(string key) => BlockHash.Parse(key);
}
