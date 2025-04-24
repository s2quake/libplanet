using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidence
{
    /// <summary>
    /// Indicates the context of evidence for creating and verification.
    /// </summary>
    public sealed class EvidenceContext : IEvidenceContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EvidenceContext"/> class.
        /// </summary>
        /// <param name="validators">
        /// Indicates the <see cref="ImmutableSortedSet<Validator>" /> where the infraction occurred at the height.
        /// </param>
        public EvidenceContext(ImmutableSortedSet<Validator> validators)
        {
            Validators = validators;
        }

        /// <summary>
        /// Indicates the <see cref="ImmutableSortedSet<Validator>" /> where the infraction occurred at the height.
        /// </summary>
        public ImmutableSortedSet<Validator> Validators { get; }
    }
}
