using Libplanet.Action;
using Libplanet.Store;

namespace Libplanet.Blockchain;

public sealed record class BlockChainOptions
{
    public Repository Repository { get; init; } = new Repository(new MemoryDatabase());

    public PolicyActions PolicyActions { get; init; } = PolicyActions.Empty;

    public TimeSpan BlockInterval { get; init; } = TimeSpan.FromSeconds(5);

    public BlockOptions BlockOptions { get; init; } = new();

    public TransactionOptions TransactionOptions { get; init; } = new();

    public EvidenceOptions EvidenceOptions { get; init; } = new();
}
