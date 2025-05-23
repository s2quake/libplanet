namespace Libplanet;

public sealed record class EvidenceOptions
{
    public long MaxEvidencePendingDuration { get; init; } = 10L;
}
