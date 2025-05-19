using Libplanet.Action;
using Libplanet.Store;

namespace Libplanet.Blockchain;

public sealed record class BlockChainOptions
{
    public Repository Store { get; init; } = new Repository(new MemoryDatabase());

    public ITable KeyValueStore { get; init; } = new MemoryTable();

    public PolicyActions PolicyActions { get; init; } = PolicyActions.Empty;

    public TimeSpan BlockInterval { get; init; } = TimeSpan.FromSeconds(5);

    public BlockOptions BlockOptions { get; init; } = new();

    public TransactionOptions TransactionOptions { get; init; } = new();

    public EvidenceOptions EvidenceOptions { get; init; } = new();
}
