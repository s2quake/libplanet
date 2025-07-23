using Libplanet.Types;

namespace Libplanet.Net;

internal sealed record class PeerServiceOptions
{
    public static PeerServiceOptions Default { get; } = new();

    public int BucketCount { get; init; } = Address.Size * 8;

    public int CapacityPerBucket { get; init; } = 16;
}
