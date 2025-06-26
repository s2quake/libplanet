using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed record class ConsensusReactorOptions
{
    public required ISigner Signer { get; init; }

    public int Port { get; init; }

    public int Workers { get; init; }

    public ImmutableArray<Peer> SeedPeers { get; init; }

    public ImmutableArray<Peer> ConsensusPeers { get; init; }

    public TimeSpan TargetBlockInterval { get; init; }

    public ConsensusOptions ContextOptions { get; init; } = ConsensusOptions.Default;
}
