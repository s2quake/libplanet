using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class PeerCollection(
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

    public Peer this[Address address] => _buckets[address][address].Peer;

    public bool AddOrUpdate(Peer peer)
        => AddOrUpdate(new PeerState { Peer = peer, LastUpdated = DateTimeOffset.UtcNow });

    public bool AddOrUpdate(PeerState peerState)
    {
        if (owner == peerState.Address)
        {
            throw new ArgumentException("Cannot add self address to the routing table.", nameof(peerState));
        }

        return _buckets[peerState.Address].AddOrUpdate(peerState);
    }

    public bool Remove(Address address) => address != owner && _buckets[address].Remove(address);

    public bool Remove(Peer peer)
    {
        var address = peer.Address;
        if (address == owner)
        {
            return false;
        }

        var bucket = _buckets[address];
        if (bucket.TryGetValue(address, out var peerState) && peerState.Peer == peer)
        {
            return bucket.Remove(address);
        }

        return false;
    }

    public bool Contains(Address address) => address != owner && _buckets[address].Contains(address);

    public bool Contains(Peer peer)
    {
        var address = peer.Address;
         if (address == owner)
        {
            return false;
        }

        var bucket = _buckets[address];
        if (bucket.TryGetValue(address, out var peerState) && peerState.Peer == peer)
        {
            return true;
        }

        return false;
    }

    public bool TryGetValue(Address address, [MaybeNullWhen(false)] out Peer peer)
    {
        if (address == owner)
        {
            peer = default;
            return false;
        }

        return _buckets[address].TryGetPeer(address, out peer);
    }

    public bool TryGetPeerState(Peer peer, [MaybeNullWhen(false)] out PeerState peerState)
    {
        var address = peer.Address;
        if (address == owner)
        {
            peerState = default;
            return false;
        }

        var bucket = _buckets[address];
        if (bucket.TryGetValue(address, out peerState) && peerState.Peer == peer)
        {
            return true;
        }

        peerState = default;
        return false;
    }

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

    internal ImmutableArray<Peer> PeersToBroadcast(ImmutableArray<Address> except, int minimum = 10)
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
                .Where(peer => !except.Contains(peer.Address))
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
