using Libplanet.Types;

namespace Libplanet;

public sealed record class EvidenceOptions
{
    public long MaxEvidencePendingDuration { get; init; } = 10L;

    internal bool IsEvidenceExpired(EvidenceBase evidence, int height)
        => evidence.Height + MaxEvidencePendingDuration + evidence.Height < height;
}
