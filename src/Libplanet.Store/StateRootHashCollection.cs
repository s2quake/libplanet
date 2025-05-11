using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

internal sealed class StateRootHashCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<BlockHash, HashDigest<SHA256>>(dictionary)
{
    protected override byte[] GetBytes(HashDigest<SHA256> value) => ModelSerializer.SerializeToBytes(value);

    protected override BlockHash GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(BlockHash key) => new(key.Bytes);

    protected override HashDigest<SHA256> GetValue(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<HashDigest<SHA256>>(bytes);
}
