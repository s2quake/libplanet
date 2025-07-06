using Libplanet.Types;

namespace Libplanet.Net.Protocols;

internal sealed class RoutingTable
{
    private readonly Address _address;

    public RoutingTable(Address address, int tableSize = Kademlia.TableSize, int bucketSize = Kademlia.BucketSize)
    {
        if (tableSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tableSize), $"The value of {nameof(tableSize)} must be positive.");
        }
        else if (bucketSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bucketSize), $"The value of {nameof(bucketSize)} must be positive.");
        }

        _address = address;
        Buckets = new BucketCollection(address, tableSize, bucketSize);
    }

    public int Count => Buckets.Sum(bucket => bucket.Count);

    public BucketCollection Buckets { get; }

    // public Bucket this[Peer peer]
    // {
    //     get
    //     {
    //         var address = peer.Address;
    //         var index = AddressUtility.CommonPrefixLength(address, _address) / Buckets.Length;
    //         return Buckets[index];
    //     }
    // }

    public ImmutableArray<Peer> Peers => [.. NonEmptyBuckets.SelectMany(bucket => bucket.Peers)];

    public ImmutableArray<PeerState> PeerStates => [.. NonEmptyBuckets.SelectMany(bucket => bucket.PeerStates)];

    internal ImmutableArray<ImmutableArray<Peer>> CachesToCheck
    {
        get
        {
            return [.. NonFullBuckets.Select(
                bucket => bucket.ReplacementCache.Values
                    .OrderBy(peerState => peerState.LastUpdated)
                    .Select(peerState => peerState.Peer)
                    .ToImmutableArray())];
        }
    }

    internal ImmutableArray<Bucket> NonFullBuckets => [.. Buckets.Where(bucket => !bucket.IsFull)];

    internal ImmutableArray<Bucket> NonEmptyBuckets => [.. Buckets.Where(bucket => !bucket.IsEmpty)];

    public void Add(Peer peer) => AddPeer(peer, DateTimeOffset.UtcNow);

    public void AddRange(IEnumerable<Peer> peers)
    {
        foreach (var peer in peers)
        {
            Add(peer);
        }
    }

    public bool Remove(Peer peer)
    {
        if (peer.Address != _address)
        {
            return Buckets[peer].Remove(peer);
        }

        return false;
    }

    public bool Contains(Peer peer)
    {
        return Buckets[peer].Contains(peer);
    }

    public Peer? GetPeer(Address addr) =>
        Peers.FirstOrDefault(peer => peer.Address.Equals(addr));

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
                    from peer in bucket.Peers
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
        if (peer.Address.Equals(_address))
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
            var rest = Peers.Except(peerList)
                .Where(peer => peer.Address != except)
                .Take(minimum - count);
            peerList.AddRange(rest);
        }

        return [.. peerList];
    }

    internal ImmutableArray<Peer> PeersToRefresh(TimeSpan maximumAge)
    {
        var dateTimeOffset = DateTimeOffset.UtcNow;
        var query = from bucket in NonEmptyBuckets
                    where bucket.IsEmpty && bucket.Tail.LastUpdated + maximumAge < dateTimeOffset
                    select bucket.Tail.Peer;

        return [.. query];
    }

    internal bool RemoveCache(Peer peer)
    {
        var bucket = Buckets[peer];
        return bucket.RemoveCache(peer);
    }

    // internal Bucket GetBucket(Peer peer)
    // {
    //     var address = peer.Address;
    //     var index = AddressUtility.CommonPrefixLength(address, _address) / Buckets.Length;
    //     return GetBucket(index);
    // }

    // internal Bucket GetBucket(int index) => Buckets[index];
}
