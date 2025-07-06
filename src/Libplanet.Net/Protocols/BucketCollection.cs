using System.Collections;
using Libplanet.Types;
using static Libplanet.Net.Protocols.AddressUtility;

namespace Libplanet.Net.Protocols;

internal sealed class BucketCollection(Address address, int count, int capacity)
    : IEnumerable<Bucket>
{
    private readonly ImmutableArray<Bucket> _buckets = Create(count, capacity);

    public Bucket this[int index] => _buckets[index];

    public Bucket this[Peer peer] => _buckets[CommonPrefixLength(peer.Address, address) / _buckets.Length];

    public int Count => _buckets.Length;

    public IEnumerator<Bucket> GetEnumerator()
    {
        foreach (var bucket in _buckets)
        {
            yield return bucket;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static ImmutableArray<Bucket> Create(int count, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        var builder = ImmutableArray.CreateBuilder<Bucket>(count);
        for (var i = 0; i < count; i++)
        {
            builder.Add(new Bucket(capacity));
        }

        return builder.ToImmutable();

    }
}
