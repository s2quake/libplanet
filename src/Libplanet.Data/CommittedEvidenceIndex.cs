namespace Libplanet.Data;

public sealed class CommittedEvidenceIndex(IDatabase database)
    : EvidenceIndexBase(database, "committed_evidence")
{
}
