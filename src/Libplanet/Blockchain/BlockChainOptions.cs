using Libplanet.Action;

namespace Libplanet.Blockchain;

public sealed record class BlockChainOptions
{
    public static BlockChainOptions Empty { get; } = new();

    public SystemActions PolicyActions { get; init; } = SystemActions.Empty;

    public TimeSpan BlockInterval { get; init; } = TimeSpan.FromSeconds(5);

    public BlockOptions BlockOptions { get; init; } = new();

    public TransactionOptions TransactionOptions { get; init; } = new();

    public EvidenceOptions EvidenceOptions { get; init; } = new();
}
