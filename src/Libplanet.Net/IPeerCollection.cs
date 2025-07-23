using Libplanet.Types;

namespace Libplanet.Net;

internal interface IPeerCollection : IEnumerable<Peer>
{
    Address Owner { get; }

    int Count { get; }

    ImmutableArray<IBucket> Buckets { get; }

    bool Contains(Address address);

    int GetBucketIndex(Address address);
}
