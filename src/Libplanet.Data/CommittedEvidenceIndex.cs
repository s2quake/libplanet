namespace Libplanet.Data;

public sealed class CommittedEvidenceIndex(IDatabase database, int cacheSize = 100)
    : EvidenceIndexBase(database, "committed_evidence", cacheSize)
{
}
