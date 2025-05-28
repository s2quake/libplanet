namespace Libplanet.Types;

public sealed record class EvidenceContext(ImmutableSortedSet<Validator> Validators)
{
}
