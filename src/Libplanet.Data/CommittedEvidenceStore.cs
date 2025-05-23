namespace Libplanet.Data;

public sealed class CommittedEvidenceStore(IDatabase database)
    : EvidenceStoreBase(database, "committed_evidence")
{
}
