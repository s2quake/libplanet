namespace Libplanet.Net;

public sealed record class PeerServiceOptions
{
    public static PeerServiceOptions Default { get; } = new();

    public int BucketCount { get; init; } = PeerCollection.BucketCount;

    public int CapacityPerBucket { get; init; } = PeerCollection.CapacityPerBucket;

    public ImmutableHashSet<Peer> SeedPeers { get; init; } = [];

    public ImmutableHashSet<Peer> KnownPeers { get; init; } = [];

    public int SearchDepth { get; init; } = 3;

    public int MinimumBroadcastTarget { get; init; } = 10;
}
