using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Crypto;

namespace Libplanet.Store;

public sealed class NonceCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<Address, long>(dictionary)
{
    protected override byte[] GetBytes(long value) => ModelSerializer.SerializeToBytes(value);

    protected override Address GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(Address key) => new(key.Bytes);

    protected override long GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<long>(bytes);
}
