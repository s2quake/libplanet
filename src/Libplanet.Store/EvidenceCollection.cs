using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Evidence;

namespace Libplanet.Store;

public sealed class EvidenceCollection(IDictionary<KeyBytes, byte[]> dictionary)
    : CollectionBase<EvidenceId, EvidenceBase>(dictionary)
{
    protected override byte[] GetBytes(EvidenceBase value) => ModelSerializer.SerializeToBytes(value);

    protected override EvidenceId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(EvidenceId key) => new(key.Bytes);

    protected override EvidenceBase GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<EvidenceBase>(bytes);
}
