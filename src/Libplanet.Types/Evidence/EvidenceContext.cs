using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidence;

public sealed class EvidenceContext(ImmutableSortedSet<Validator> validators)
    : IEvidenceContext
{
    public ImmutableSortedSet<Validator> Validators { get; } = validators;
}
