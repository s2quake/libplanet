using System.Collections.Generic;
using Libplanet.Blockchain;
using Libplanet.Types.Consensus;

namespace Libplanet.Net.Consensus
{
    /// <summary>
    /// Pool that gathers duplicated <see cref="Vote"/> pairs.
    /// </summary>
    internal sealed class EvidenceCollector
    {
        private readonly List<EvidenceException> _evidenceExceptions
            = new List<EvidenceException>();

        public void Handle(EvidenceException exception)
        {
            _evidenceExceptions.Add(exception);
        }

        public IEnumerable<Evidence> Exhaust(BlockChain blockChain)
        {
            var evidenceExceptions = _evidenceExceptions.ToArray();
            _evidenceExceptions.Clear();
            foreach (var evidenceException in evidenceExceptions)
            {
                yield return evidenceException.CreateEvidence(blockChain);
            }
        }
    }
}
