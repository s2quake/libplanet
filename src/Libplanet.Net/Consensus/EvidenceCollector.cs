using Libplanet.Types;

namespace Libplanet.Net.Consensus;

internal sealed class EvidenceCollector
{
    private readonly List<EvidenceException> _exceptionList = [];

    public void Add(EvidenceException exception) => _exceptionList.Add(exception);

    public void AddRange(EvidenceException[] exceptions) => _exceptionList.AddRange(exceptions);

    public EvidenceException[] Flush()
    {
        var evidenceExceptions = _exceptionList.ToArray();
        _exceptionList.Clear();
        return evidenceExceptions;
    }
}
