namespace Libplanet.Net.Consensus;

public sealed record class GossipOptions
{
    public ImmutableArray<Peer> Validators { get; init; } = [];

    public ImmutableArray<Peer> Seeds { get; init; } = [];

    public TimeSpan RebuildTableInterval { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan RefreshTableInterval { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan RefreshLifespan { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(1);
}
