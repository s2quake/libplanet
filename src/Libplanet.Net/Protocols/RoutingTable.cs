using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Protocols;

internal sealed class RoutingTable(
    Address owner,
    int bucketCount = RoutingTable.BucketCount,
    int capacityPerBucket = RoutingTable.CapacityPerBucket) : IEnumerable<PeerState>
{
    public const int BucketCount = Address.Size * 8;
    public const int CapacityPerBucket = 16;
    private readonly ReaderWriterLockSlim _lock = new();

    public BucketCollection Buckets { get; } = new(owner, bucketCount, capacityPerBucket);

    public Address Owner => owner;

    public int Count
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return Buckets.Sum(item => item.Count);
        }
    }

    public IEnumerable<Peer> Peers
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return Buckets.SelectMany(bucket => bucket.Select(item => item.Peer));
        }
    }

    public PeerState this[Peer peer] => Buckets[peer.Address][peer.Address];

    public bool AddOrUpdate(Peer peer)
    {
        return AddOrUpdate(new PeerState { Peer = peer, LastUpdated = DateTimeOffset.UtcNow });
    }

    public bool AddOrUpdate(PeerState peerState)
    {
        if (owner == peerState.Address)
        {
            throw new ArgumentException("Cannot add self address to the routing table.", nameof(peerState));
        }

        return Buckets[peerState.Address].AddOrUpdate(peerState);
    }

    public bool Remove(Peer peer)
    {
        if (peer.Address != owner)
        {
            return Buckets[peer].Remove(peer);
        }

        return false;
    }

    public bool Contains(Peer peer) => Buckets[peer].Contains(peer);

    public bool TryGetPeer(Address address, [MaybeNullWhen(false)] out Peer peer)
        => Buckets[address].TryGetPeer(address, out peer);

    public bool TryGetValue(Address address, [MaybeNullWhen(false)] out PeerState value)
        => Buckets[address].TryGetValue(address, out value);

    public void Clear()
    {
        foreach (var bucket in Buckets)
        {
            bucket.Clear();
        }
    }

    public ImmutableArray<Peer> GetNeighbors(Address target, int k, bool includeTarget = false)
    {
        // Select maximum k * 2 peers excluding the target itself.
        var query = from bucket in Buckets
                    where !bucket.IsEmpty
                    from peerState in bucket
                    where includeTarget || peerState.Address != target
                    orderby AddressUtility.GetDistance(target, peerState.Address)
                    select peerState.Peer;
        var peers = query.ToImmutableArray();
        var containsTarget = peers.Any(peer => peer.Address == target);
        var count = (includeTarget && containsTarget) ? (k * 2) + 1 : k * 2;

        return [.. peers.Take(count)];
    }

    internal void AddRange(IEnumerable<Peer> peers)
    {
        foreach (var peer in peers)
        {
            AddOrUpdate(peer);
        }
    }

    internal ImmutableArray<Peer> PeersToBroadcast(Address except, int minimum = 10)
    {
        var query = from bucket in Buckets
                    where !bucket.IsEmpty
                    let peer = bucket.TryGetRandomPeer(except, out var v) ? v : null
                    where peer is not null
                    select peer;
        var peerList = query.ToList();
        var count = peerList.Count;
        if (count < minimum)
        {
            var rest = Peers.Except(peerList)
                .Where(peer => peer.Address != except)
                .Take(minimum - count);
            peerList.AddRange(rest);
        }

        return [.. peerList.Select(item => item)];
    }

    internal ImmutableArray<Peer> GetStalePeers(TimeSpan staleThreshold)
    {
        var query = from bucket in Buckets
                    where !bucket.IsEmpty && bucket.Oldest.IsStale(staleThreshold)
                    select bucket.Oldest.Peer;

        return [.. query];
    }

    public IEnumerator<PeerState> GetEnumerator()
    {
        foreach (var bucket in Buckets)
        {
            foreach (var pair in bucket)
            {
                yield return pair;
            }
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
