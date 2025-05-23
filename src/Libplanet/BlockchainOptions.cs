using Libplanet.State;

namespace Libplanet;

public sealed record class BlockchainOptions
{
    public static BlockchainOptions Empty { get; } = new();

    public SystemActions PolicyActions { get; init; } = SystemActions.Empty;

    public TimeSpan BlockInterval { get; init; } = TimeSpan.FromSeconds(5);

    public BlockOptions BlockOptions { get; init; } = new();

    public TransactionOptions TransactionOptions { get; init; } = new();

    public EvidenceOptions EvidenceOptions { get; init; } = new();
}
