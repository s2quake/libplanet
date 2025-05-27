namespace Libplanet.Data;

public sealed class PendingEvidenceIndex(IDatabase database)
    : EvidenceIndexBase(database, "pending_evidence")
{
}
