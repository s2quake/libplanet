using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;

namespace Libplanet.Store;

public sealed class CommittedEvidenceStore(IDatabase database)
    : CollectionBase<EvidenceId, EvidenceBase>(database.GetOrAdd("committed_evidence"))
{
    public void Add(Block block)
    {
        foreach (var evidence in block.Evidences)
        {
            Add(evidence.Id, evidence);
        }
    }

    public void RemoveRange(IEnumerable<EvidenceId> evidenceIds)
    {
        foreach (var evidenceId in evidenceIds)
        {
            Remove(evidenceId);
        }
    }

    protected override byte[] GetBytes(EvidenceBase value) => ModelSerializer.SerializeToBytes(value);

    protected override EvidenceId GetKey(KeyBytes keyBytes) => new(keyBytes.Bytes);

    protected override KeyBytes GetKeyBytes(EvidenceId key) => new(key.Bytes);

    protected override EvidenceBase GetValue(byte[] bytes) => ModelSerializer.DeserializeFromBytes<EvidenceBase>(bytes);
}
