namespace Libplanet;

public sealed record class EvidenceOptions
{
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromMinutes(1);

    public int ExpiresInBlocks { get; init; } = 10;
}
