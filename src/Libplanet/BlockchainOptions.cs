using Libplanet.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Libplanet;

public sealed record class BlockchainOptions
{
    public static BlockchainOptions Empty { get; } = new();

    public SystemActions SystemActions { get; init; } = SystemActions.Empty;

    public BlockOptions BlockOptions { get; init; } = new();

    public TransactionOptions TransactionOptions { get; init; } = new();

    // public EvidenceOptions EvidenceOptions { get; init; } = new();

    public ILogger<Blockchain> Logger { get; init; } = NullLogger<Blockchain>.Instance;
}
