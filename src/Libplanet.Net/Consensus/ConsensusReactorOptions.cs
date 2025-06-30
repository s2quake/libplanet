namespace Libplanet.Net.Consensus;

public sealed record class ConsensusReactorOptions
{
    public int Port { get; init; }

    public int Workers { get; init; }

    public ImmutableArray<Peer> Seeds { get; init; } = [];

    public ImmutableArray<Peer> Validators { get; init; } = [];

    public TimeSpan TargetBlockInterval { get; init; }

    public ConsensusOptions ConsensusOptions { get; init; } = ConsensusOptions.Default;
}
