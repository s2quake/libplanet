using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Protocols;

internal sealed class RoutingTable(
    Address address,
    int bucketCount = Kademlia.BucketCount,
    int bucketCapacity = Kademlia.BucketCapacity)
    : IEnumerable<PeerState>
{
    public BucketCollection Buckets { get; } = new BucketCollection(address, bucketCount, bucketCapacity);

    // public IEnumerable<Peer> Keys => Buckets.SelectMany(bucket => bucket.Keys);

    // public IEnumerable<PeerState> Values => Buckets.SelectMany(bucket => bucket.Values);

    public int Count => Buckets.Sum(item => item.Count);

    public IEnumerable<Peer> Peers => Buckets.SelectMany(bucket => bucket.Select(item => item.Peer));

    public PeerState this[Peer peer] => throw new NotImplementedException();

    public bool Add(Peer key) => AddOrUpdate(key, DateTimeOffset.UtcNow);

    public void AddRange(IEnumerable<Peer> peers)
    {
        foreach (var peer in peers)
        {
            Add(peer);
        }
    }

    public bool Remove(Peer peer)
    {
        if (peer.Address != address)
        {
            return Buckets[peer].Remove(peer);
        }

        return false;
    }

    public bool Contains(Peer peer) => Buckets[peer].Contains(peer);

    public Peer? GetPeer(Address addr) => Buckets[addr][addr].Peer;
    // Keys.FirstOrDefault(peer => peer.Address.Equals(addr));

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

    public void Check(Peer peer, TimeSpan latency) => Buckets[peer].Check(peer, latency);

    internal bool AddOrUpdate(Peer peer, DateTimeOffset updated)
    {
        if (peer.Address.Equals(address))
        {
            return false;
        }

        return Buckets[peer].AddOrUpdate(peer, updated);
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

    internal ImmutableArray<Peer> PeersToRefresh(TimeSpan maximumAge)
    {
        var dateTimeOffset = DateTimeOffset.UtcNow;
        var query = from bucket in Buckets
                    where !bucket.IsEmpty
                    where bucket.IsEmpty && bucket.Tail.LastUpdated + maximumAge < dateTimeOffset
                    select bucket.Tail.Peer;

        return [.. query];
    }

    // public bool TryGetValue(Peer key, [MaybeNullWhen(false)] out PeerState value)
    //     => Buckets[key].TryGetValue(key, out value);

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
}
