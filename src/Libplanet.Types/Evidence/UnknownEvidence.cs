using Bencodex.Types;

namespace Libplanet.Types.Evidence;

internal sealed record class UnknownEvidence : EvidenceBase
{
    protected override void OnVerify(IEvidenceContext evidenceContext)
    {
    }
}
