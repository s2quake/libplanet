using System.Collections;
using Libplanet.Types;
using static Libplanet.Net.Protocols.AddressUtility;

namespace Libplanet.Net.Protocols;

internal sealed class BucketCollection(Address owner, ImmutableArray<Bucket> _buckets)
    : IEnumerable<Bucket>
{
    public Bucket this[int index] => _buckets[index];

    public Bucket this[Peer peer] => this[peer.Address];

    public Bucket this[Address address] => _buckets[IndexOf(address)];

    public int Count => _buckets.Length;

    public int IndexOf(Address address)
    {
        if (_buckets.Length == RoutingTable.BucketCount)
        {
            return CommonPrefixLength(address, owner);
        }

        var factor = (double)_buckets.Length / RoutingTable.BucketCount;
        return (int)(CommonPrefixLength(address, owner) * factor);
    }

    public IEnumerator<Bucket> GetEnumerator()
    {
        foreach (var bucket in _buckets)
        {
            yield return bucket;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
