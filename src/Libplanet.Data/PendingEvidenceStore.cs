namespace Libplanet.Data;

public sealed class PendingEvidenceStore(IDatabase database)
    : EvidenceStoreBase(database, "pending_evidence")
{
}
