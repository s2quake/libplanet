namespace Libplanet.Data;

public sealed class PendingEvidenceIndex(IDatabase database, int cacheSize = 100)
    : EvidenceIndexBase(database, "pending_evidence", cacheSize)
{
}
