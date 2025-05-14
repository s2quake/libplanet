namespace Libplanet.Store;

public sealed class PendingEvidenceStore(IDatabase database)
    : EvidenceStoreBase(database, "pending_evidence")
{
}
