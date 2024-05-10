using System.Collections.Generic;
using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidences
{
    /// <summary>
    /// Pool that gathers duplicated <see cref="Vote"/> pairs.
    /// </summary>
    public sealed class EvidenceCollector
    {
        private readonly List<EvidenceException> _exceptionList
            = new List<EvidenceException>();

        public void Handle(EvidenceException exception)
        {
            _exceptionList.Add(exception);
        }

        public IEnumerable<Evidence> Exhaust(IEvidenceContext evidenceContext)
        {
            var evidenceExceptions = _exceptionList.ToArray();
            _exceptionList.Clear();
            foreach (var evidenceException in evidenceExceptions)
            {
                yield return evidenceException.CreateEvidence(evidenceContext);
            }
        }
    }
}
