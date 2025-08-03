
namespace Libplanet.Net.Consensus;

public sealed record class ConsensusServiceOptions
{
    public int Workers { get; init; }

    public ImmutableHashSet<Peer> Seeds { get; init; } = [];

    public ImmutableHashSet<Peer> Validators { get; init; } = [];

    public TimeSpan TargetBlockInterval { get; init; }

    public ConsensusOptions ConsensusOptions { get; init; } = ConsensusOptions.Default;
}
