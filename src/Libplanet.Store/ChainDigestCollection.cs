using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class ChainDigestCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<Guid, ChainDigest>(dictionary)
{
    public void Set(Guid chainID, Block block)
    {
        if (!TryGetValue(chainID, out var chainDigest))
        {
            chainDigest = new ChainDigest
            {
                Id = chainID,
            };
        }

        this[chainID] = chainDigest with
        {
            Height = block.Height,
        };
    }

    protected override byte[] GetBytes(ChainDigest value) => ModelSerializer.SerializeToBytes(value);

    protected override Guid GetKey(KeyBytes keyBytes) => new(keyBytes.AsSpan());

    protected override KeyBytes GetKeyBytes(Guid key) => new(key.ToByteArray());

    protected override ChainDigest GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<ChainDigest>(bytes);
}
