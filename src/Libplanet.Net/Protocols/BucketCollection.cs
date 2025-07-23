using System.Collections;
using Libplanet.Types;
using static Libplanet.Net.Protocols.AddressUtility;

namespace Libplanet.Net.Protocols;

internal sealed class BucketCollection(Address owner, int bucketCount, int capacityPerBucket)
    : IEnumerable<Bucket>
{
    private readonly ImmutableArray<Bucket> _buckets = Create(bucketCount, capacityPerBucket);

    public Bucket this[int index] => _buckets[index];

    public Bucket this[Address address] => _buckets[IndexOf(address)];

    public int Count => _buckets.Length;

    public int CapacityPerBucket => capacityPerBucket;

    public int IndexOf(Address address)
    {
        if (_buckets.Length == PeerCollection.BucketCount)
        {
            return CommonPrefixLength(address, owner);
        }

        var factor = (double)_buckets.Length / PeerCollection.BucketCount;
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

    private static ImmutableArray<Bucket> Create(int bucketCount, int capacityPerBucket)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bucketCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityPerBucket);

        var builder = ImmutableArray.CreateBuilder<Bucket>(bucketCount);
        for (var i = 0; i < bucketCount; i++)
        {
            builder.Add(new Bucket(capacityPerBucket));
        }

        return builder.ToImmutable();
    }
}
