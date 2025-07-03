using Libplanet.Types;

namespace Libplanet.Net.Protocols;

public sealed class RoutingTable
{
    private readonly Address _address;
    private readonly KademliaBucket[] _buckets;

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
        BucketSize = bucketSize;

        var random = new Random();
        _buckets = new KademliaBucket[tableSize];
        for (int i = 0; i < tableSize; i++)
        {
            _buckets[i] = new KademliaBucket(BucketSize, random);
        }
    }

    public int BucketSize { get; }

    public int Count => _buckets.Sum(bucket => bucket.Count);

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

    internal ImmutableArray<KademliaBucket> NonFullBuckets => [.. _buckets.Where(bucket => !bucket.IsFull)];

    internal ImmutableArray<KademliaBucket> NonEmptyBuckets => [.. _buckets.Where(bucket => !bucket.IsEmpty)];

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
            return GetBucker(peer).Remove(peer);
        }

        return false;
    }

    public bool Contains(Peer peer)
    {
        return GetBucker(peer).Contains(peer);
    }

    public Peer? GetPeer(Address addr) =>
        Peers.FirstOrDefault(peer => peer.Address.Equals(addr));

    public void Clear()
    {
        foreach (var bucket in _buckets)
        {
            bucket.Clear();
        }
    }

    public ImmutableArray<Peer> Neighbors(Peer target, int k, bool includeTarget)
        => Neighbors(target.Address, k, includeTarget);

    public ImmutableArray<Peer> Neighbors(Address target, int k, bool includeTarget)
    {
        var sorted = _buckets
            .Where(b => !b.IsEmpty)
            .SelectMany(b => b.Peers)
            .ToImmutableArray();

        sorted = [.. Kademlia.SortByDistance(sorted, target)];

        // Select maximum k * 2 peers excluding the target itself.
        bool containsTarget = sorted.Any(peer => peer.Address.Equals(target));
        int maxCount = (includeTarget && containsTarget) ? (k * 2) + 1 : k * 2;

        IEnumerable<Peer> peers = includeTarget
            ? sorted
            : sorted.Where(peer => !peer.Address.Equals(target));

        return [.. peers.Take(maxCount)];
    }

    public void Check(Peer peer, DateTimeOffset startTime, DateTimeOffset endTime)
        => GetBucker(peer).Check(peer, startTime, endTime);

    internal void AddPeer(Peer peer, DateTimeOffset updated)
    {
        if (peer.Address.Equals(_address))
        {
            throw new ArgumentException("A node is disallowed to add itself to its routing table.", nameof(peer));
        }

        GetBucker(peer).AddPeer(peer, updated);
    }

    internal ImmutableArray<Peer> PeersToBroadcast(Address except, int minimum = 10)
    {
        var query = from bucket in NonEmptyBuckets
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
        var bucket = GetBucker(peer);
        return bucket.RemoveCache(peer);
    }

    internal KademliaBucket GetBucker(Peer peer)
    {
        var index = GetBucketIndexOf(peer.Address);
        return GetBucker(index);
    }

    internal KademliaBucket GetBucker(int index) => _buckets[index];

    internal int GetBucketIndexOf(Address address)
    {
        var length = Kademlia.CommonPrefixLength(address, _address);
        return Math.Min(length, _buckets.Length - 1);
    }
}
