using Libplanet.Net.Protocols;

namespace Libplanet.Net.Options;

public sealed record class SwarmOptions
{
    public TimeSpan BlockDemandLifespan { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan? MessageTimestampBuffer { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan RefreshPeriod { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan RefreshLifespan { get; init; } = TimeSpan.FromSeconds(60);

    public ImmutableHashSet<Peer> StaticPeers { get; init; } = [];

    public TimeSpan StaticPeersMaintainPeriod { get; init; } = TimeSpan.FromSeconds(10);

    public int MinimumBroadcastTarget { get; init; } = 10;

    public TimeSpan BlockBroadcastInterval { get; init; } = TimeSpan.FromMilliseconds(15_000);

    public TimeSpan TxBroadcastInterval { get; init; } = TimeSpan.FromMilliseconds(5_000);

    public TimeSpan EvidenceBroadcastInterval { get; init; } = TimeSpan.FromMilliseconds(5_000);

    public int TableSize { get; init; } = Kademlia.TableSize;

    public int BucketSize { get; init; } = Kademlia.BucketSize;

    public int MaximumPollPeers { get; init; } = int.MaxValue;

    public TimeSpan TipLifespan { get; init; } = TimeSpan.FromSeconds(60);

    public TransportOptions TransportOptions { get; init; } = new();

    public BootstrapOptions BootstrapOptions { get; init; } = new BootstrapOptions();

    public PreloadOptions PreloadOptions { get; init; } = new PreloadOptions();

    public TimeoutOptions TimeoutOptions { get; init; } = new TimeoutOptions();

    public TaskRegulationOptions TaskRegulationOptions { get; init; } = new TaskRegulationOptions();
}
