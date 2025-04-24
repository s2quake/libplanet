using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidence;

public interface IEvidenceContext
{
    ImmutableSortedSet<Validator> Validators { get; }
}
