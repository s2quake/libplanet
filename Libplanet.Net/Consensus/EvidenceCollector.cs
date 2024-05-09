using System.Collections.Generic;
using Libplanet.Blockchain;
using Libplanet.Types.Consensus;
using Libplanet.Types.Evidences;

namespace Libplanet.Net.Consensus
{
    /// <summary>
    /// Pool that gathers duplicated <see cref="Vote"/> pairs.
    /// </summary>
    internal sealed class EvidenceCollector
    {
        private readonly List<EvidenceException> _exceptionList
            = new List<EvidenceException>();

        public void Handle(EvidenceException exception)
        {
            _exceptionList.Add(exception);
        }

        public IEnumerable<Evidence> Exhaust(BlockChain blockChain)
        {
            var evidenceExceptions = _exceptionList.ToArray();
            _exceptionList.Clear();
            foreach (var evidenceException in evidenceExceptions)
            {
                yield return evidenceException.CreateEvidence(blockChain);
            }
        }
    }
}
