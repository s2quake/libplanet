using Libplanet.Serialization;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public sealed class ChainDigestCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<Guid, ChainDigest>(dictionary)
{
    protected override byte[] GetBytes(ChainDigest value) => ModelSerializer.SerializeToBytes(value);

    protected override Guid GetKey(KeyBytes keyBytes) => new(keyBytes.AsSpan());

    protected override KeyBytes GetKeyBytes(Guid key) => new(key.ToByteArray());

    protected override ChainDigest GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<ChainDigest>(bytes);
}
