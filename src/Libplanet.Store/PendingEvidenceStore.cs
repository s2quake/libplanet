using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;

namespace Libplanet.Store;

public sealed class PendingEvidenceStore(IDatabase database)
    : CollectionBase<EvidenceId, EvidenceBase>(database.GetOrAdd("pending_evidence"))
{
    public void Add(Block block)
    {
        foreach (var evidence in block.Evidences)
        {
            Remove(evidence.Id);
        }
    }

    protected override byte[] GetBytes(EvidenceBase value) => ModelSerializer.SerializeToBytes(value);

    protected override EvidenceId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(EvidenceId key) => new(key.Bytes);

    protected override EvidenceBase GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<EvidenceBase>(bytes);
}
