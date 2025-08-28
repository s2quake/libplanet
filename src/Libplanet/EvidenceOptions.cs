using Libplanet.Types;

namespace Libplanet;

public sealed record class EvidenceOptions
{
    public int ExpiresInBlocks { get; init; } = 10;

    internal void Validate(Block block, EvidenceBase evidence)
    {
        if (IsHeightExpired(evidence, block.Height))
        {
            throw new ArgumentException(
                $"Evidence with ID {evidence.Id} is too old to be included in the block.",
                nameof(evidence));
        }
    }

    internal bool IsHeightExpired(EvidenceBase evidence, int height) => evidence.Height + ExpiresInBlocks < height;
}
