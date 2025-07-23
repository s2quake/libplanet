using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

internal sealed class PeerCollection(
    Address owner,
    int bucketCount = PeerCollection.BucketCount,
    int capacityPerBucket = PeerCollection.CapacityPerBucket) : IPeerCollection
{
    public const int BucketCount = Address.Size * 8;
    public const int CapacityPerBucket = 16;
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly BucketCollection _buckets = new(owner, bucketCount, capacityPerBucket);
    private ImmutableArray<IBucket>? _bucketArray;


    public Address Owner => owner;

    public BucketCollection Buckets => _buckets;

    public int Count
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return _buckets.Sum(item => item.Count);
        }
    }

    ImmutableArray<IBucket> IPeerCollection.Buckets => _bucketArray ??= [.. _buckets.Cast<IBucket>()];

    public PeerState this[Peer peer] => _buckets[peer.Address][peer.Address];

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

        return _buckets[peerState.Address].AddOrUpdate(peerState);
    }

    public bool Remove(Peer peer)
    {
        if (peer.Address != owner)
        {
            return _buckets[peer.Address].Remove(peer);
        }

        return false;
    }

    public bool Contains(Address address) => _buckets[address].Contains(address);

    public bool TryGetPeer(Address address, [MaybeNullWhen(false)] out Peer peer)
        => _buckets[address].TryGetPeer(address, out peer);

    public bool TryGetValue(Address address, [MaybeNullWhen(false)] out PeerState value)
        => _buckets[address].TryGetValue(address, out value);

    public void Clear()
    {
        foreach (var bucket in _buckets)
        {
            bucket.Clear();
        }
    }

    public PeerState GetState(Address address) => _buckets[address][address];

    public ImmutableArray<Peer> GetNeighbors(Address target, int k, bool includeTarget = false)
    {
        // Select maximum k * 2 peers excluding the target itself.
        var query = from bucket in _buckets
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

    // internal void AddRange(IEnumerable<Peer> peers)
    // {
    //     foreach (var peer in peers)
    //     {
    //         AddOrUpdate(peer);
    //     }
    // }

    internal ImmutableArray<Peer> PeersToBroadcast(Address except, int minimum = 10)
    {
        var query = from bucket in _buckets
                    where !bucket.IsEmpty
                    let peer = bucket.TryGetRandomPeer(except, out var v) ? v : null
                    where peer is not null
                    select peer;
        var peerList = query.ToList();
        var count = peerList.Count;
        if (count < minimum)
        {
            var rest = this.Except(peerList)
                .Where(peer => peer.Address != except)
                .Take(minimum - count);
            peerList.AddRange(rest);
        }

        return [.. peerList.Select(item => item)];
    }

    internal ImmutableArray<Peer> GetStalePeers(TimeSpan staleThreshold)
    {
        var query = from bucket in _buckets
                    where !bucket.IsEmpty && bucket.Oldest.IsStale(staleThreshold)
                    select bucket.Oldest.Peer;

        return [.. query];
    }

    public IEnumerator<Peer> GetEnumerator()
    {
        foreach (var bucket in _buckets)
        {
            foreach (var peerState in bucket)
            {
                yield return peerState.Peer;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int GetBucketIndex(Address address)
    {
        return _buckets.IndexOf(address);
    }
}
