using Libplanet.Net.Options;

namespace Libplanet.Net.Consensus;

public sealed record class ConsensusReactorOptions
{
    public TransportOptions TransportOptions { get; init; } = new();

    public int Workers { get; init; }

    public ImmutableArray<Peer> Seeds { get; init; } = [];

    public ImmutableArray<Peer> Validators { get; init; } = [];

    public TimeSpan TargetBlockInterval { get; init; }

    public ConsensusOptions ConsensusOptions { get; init; } = ConsensusOptions.Default;

    public GossipOptions GossipOptions { get; init; } = new GossipOptions();
}
