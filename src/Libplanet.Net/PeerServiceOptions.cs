using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class PeerServiceOptions
{
    public static PeerServiceOptions Default { get; } = new();

    public int BucketCount { get; init; } = PeerCollection.BucketCount;

    public int CapacityPerBucket { get; init; } = PeerCollection.CapacityPerBucket;
}
