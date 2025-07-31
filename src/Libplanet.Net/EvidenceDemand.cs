using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class EvidenceDemand(Peer Peer, ImmutableHashSet<EvidenceId> EvidenceIds)
{
}
