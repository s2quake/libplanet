using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Protocols;

internal sealed class RoutingTable(
    Address address,
    int tableSize = Kademlia.TableSize,
    int bucketSize = Kademlia.BucketSize)
    : IReadOnlyDictionary<Peer, PeerState>
{
    public BucketCollection Buckets { get; } = new BucketCollection(address, tableSize, bucketSize);

    internal ImmutableArray<Peer> CachesToCheck
    {
        get
        {
            var query = from bucket in Buckets
                        where !bucket.IsFull
                        from peerState in bucket.ReplacementCache.Values
                        orderby peerState.LastUpdated
                        select peerState.Peer;
            return [.. query];
        }
    }

    public IEnumerable<Peer> Keys => Buckets.SelectMany(bucket => bucket.Keys);

    public IEnumerable<PeerState> Values => Buckets.SelectMany(bucket => bucket.Values);

    public int Count => Buckets.Sum(item => item.Count);

    public PeerState this[Peer key] => throw new NotImplementedException();

    public void Add(Peer key) => AddPeer(key, DateTimeOffset.UtcNow);

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

    public bool ContainsKey(Peer key) => Buckets[key].ContainsKey(key);

    public Peer? GetPeer(Address addr) =>
        Keys.FirstOrDefault(peer => peer.Address.Equals(addr));

    public void Clear()
    {
        foreach (var bucket in Buckets)
        {
            bucket.Clear();
        }
    }

    public ImmutableArray<Peer> Neighbors(Peer target, int k, bool includeTarget)
        => Neighbors(target.Address, k, includeTarget);

    public ImmutableArray<Peer> Neighbors(Address target, int k, bool includeTarget)
    {
        var query = from bucket in Buckets
                    where !bucket.IsEmpty
                    from peer in bucket.Keys
                    orderby AddressUtility.GetDistance(target, peer.Address)
                    select peer;
        var sorted = query.ToImmutableArray();

        // Select maximum k * 2 peers excluding the target itself.
        var containsTarget = sorted.Any(peer => peer.Address.Equals(target));
        var maximum = (includeTarget && containsTarget) ? (k * 2) + 1 : k * 2;
        var peers = includeTarget ? sorted : sorted.Where(peer => peer.Address != target);

        return [.. peers.Take(maximum)];
    }

    public void Check(Peer peer, DateTimeOffset startTime, TimeSpan latency)
        => Buckets[peer].Check(peer, startTime, latency);

    internal void AddPeer(Peer peer, DateTimeOffset updated)
    {
        if (peer.Address.Equals(address))
        {
            throw new ArgumentException("A node is disallowed to add itself to its routing table.", nameof(peer));
        }

        Buckets[peer].AddOrUpdate(peer, updated);
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
            var rest = Keys.Except(peerList)
                .Where(peer => peer.Address != except)
                .Take(minimum - count);
            peerList.AddRange(rest);
        }

        return [.. peerList];
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

    internal bool RemoveCache(Peer peer) => Buckets[peer].RemoveCache(peer);

    public bool TryGetValue(Peer key, [MaybeNullWhen(false)] out PeerState value)
        => Buckets[key].TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<Peer, PeerState>> GetEnumerator()
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
