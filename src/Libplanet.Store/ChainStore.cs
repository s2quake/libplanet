using Libplanet.Serialization;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public sealed class ChainStore(Store store, IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<Guid, Chain>(dictionary)
{
    protected override byte[] GetBytes(Chain value)
    {
        var chainDigest = new ChainDigest
        {
            Id = value.Id,
            Height = value.Blocks.Count,
            BlockCommit = value.BlockCommit,
        };

        return ModelSerializer.SerializeToBytes(chainDigest);
    }

    protected override Guid GetKey(KeyBytes keyBytes) => new(keyBytes.AsSpan());

    protected override KeyBytes GetKeyBytes(Guid key) => new(key.ToByteArray());

    protected override Chain GetValue(byte[] bytes)
    {
        var digest = ModelSerializer.DeserializeFromBytes<ChainDigest>(bytes);
        return new Chain(store, digest.Id) { BlockCommit = digest.BlockCommit };
    }
}
