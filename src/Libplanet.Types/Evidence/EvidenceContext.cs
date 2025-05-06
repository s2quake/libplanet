using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidence;

public sealed record class EvidenceContext(ImmutableSortedSet<Validator> Validators)
{
}
