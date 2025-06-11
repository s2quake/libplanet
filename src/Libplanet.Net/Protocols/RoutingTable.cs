using Libplanet.Types;

namespace Libplanet.Net.Protocols;

public sealed class RoutingTable : IRoutingTable
{
    private readonly Address _address;
    private readonly KBucket[] _buckets;

    public RoutingTable(
        Address address,
        int tableSize = Kademlia.TableSize,
        int bucketSize = Kademlia.BucketSize)
    {
        if (tableSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tableSize),
                $"The value of {nameof(tableSize)} must be positive.");
        }
        else if (bucketSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bucketSize),
                $"The value of {nameof(bucketSize)} must be positive.");
        }

        _address = address;
        BucketSize = bucketSize;

        var random = new Random();
        _buckets = new KBucket[TableSize];
        for (int i = 0; i < tableSize; i++)
        {
            _buckets[i] = new KBucket(BucketSize, random);
        }
    }

    public int TableSize => _buckets.Length;

    public int BucketSize { get; }

    public int Count => _buckets.Sum(bucket => bucket.Count);

    public IReadOnlyList<BoundPeer> Peers =>
        NonEmptyBuckets.SelectMany(bucket => bucket.Peers).ToImmutableArray();

    public IReadOnlyList<PeerState> PeerStates =>
        NonEmptyBuckets.SelectMany(bucket => bucket.PeerStates).ToImmutableArray();

    internal IReadOnlyList<IReadOnlyList<BoundPeer>> CachesToCheck
    {
        get
        {
            return NonFullBuckets.Select(
                bucket => bucket.ReplacementCache.Values
                    .OrderBy(peerState => peerState.LastUpdated)
                    .Select(peerState => peerState.Peer)
                    .ToArray())
            .ToArray();
        }
    }

    internal IReadOnlyList<KBucket> NonFullBuckets
    {
        get
        {
            return _buckets.Where(bucket => !bucket.IsFull).ToArray();
        }
    }

    internal IReadOnlyList<KBucket> NonEmptyBuckets
    {
        get
        {
            return _buckets.Where(bucket => !bucket.IsEmpty).ToArray();
        }
    }

    public void AddPeer(BoundPeer peer) => AddPeer(peer, DateTimeOffset.UtcNow);

    public bool RemovePeer(BoundPeer peer)
    {
        if (peer.Address.Equals(_address))
        {
            throw new ArgumentException(
                "A node is disallowed to remove itself from its routing table.",
                nameof(peer));
        }

        return BucketOf(peer).RemovePeer(peer);
    }

    public bool Contains(BoundPeer peer)
    {
        return BucketOf(peer).Contains(peer);
    }

    public BoundPeer? GetPeer(Address addr) =>
        Peers.FirstOrDefault(peer => peer.Address.Equals(addr));

    public void Clear()
    {
        foreach (KBucket bucket in _buckets)
        {
            bucket.Clear();
        }
    }

    public IReadOnlyList<BoundPeer> Neighbors(BoundPeer target, int k, bool includeTarget)
        => Neighbors(target.Address, k, includeTarget);

    public IReadOnlyList<BoundPeer> Neighbors(Address target, int k, bool includeTarget)
    {
        // TODO: Should include static peers?
        var sorted = _buckets
            .Where(b => !b.IsEmpty)
            .SelectMany(b => b.Peers)
            .ToList();

        sorted = Kademlia.SortByDistance(sorted, target).ToList();

        // Select maximum k * 2 peers excluding the target itself.
        bool containsTarget = sorted.Any(peer => peer.Address.Equals(target));
        int maxCount = (includeTarget && containsTarget) ? (k * 2) + 1 : k * 2;

        IEnumerable<BoundPeer> peers = includeTarget
            ? sorted
            : sorted.Where(peer => !peer.Address.Equals(target));

        return peers.Take(maxCount).ToArray();
    }

    public void Check(BoundPeer peer, DateTimeOffset start, DateTimeOffset end)
        => BucketOf(peer).Check(peer, start, end);

    internal void AddPeer(BoundPeer peer, DateTimeOffset updated)
    {
        if (peer.Address.Equals(_address))
        {
            throw new ArgumentException(
                "A node is disallowed to add itself to its routing table.",
                nameof(peer));
        }

        BucketOf(peer).AddPeer(peer, updated);
    }

    internal IReadOnlyList<BoundPeer> PeersToBroadcast(Address? except, int min = 10)
    {
        List<BoundPeer> peers = NonEmptyBuckets
            .Select(bucket => bucket.GetRandomPeer(except))
            .OfType<BoundPeer>()
            .ToList();
        int count = peers.Count;
        if (count < min)
        {
            peers.AddRange(Peers
                .Where(peer =>
                    !peers.Contains(peer) &&
                        (!(except is Address e) || !peer.Address.Equals(e)))
                .Take(min - count));
        }

        return peers;
    }

    internal IReadOnlyList<BoundPeer> PeersToRefresh(TimeSpan maxAge) => NonEmptyBuckets
        .Where(bucket =>
            bucket.Tail is PeerState peerState &&
                peerState.LastUpdated + maxAge < DateTimeOffset.UtcNow)
        .Select(bucket => bucket.Tail!.Peer)
        .ToList();

    internal bool RemoveCache(BoundPeer peer)
    {
        KBucket bucket = BucketOf(peer);
        return bucket.ReplacementCache.Remove(peer);
    }

    internal KBucket BucketOf(BoundPeer peer)
    {
        int index = GetBucketIndexOf(peer.Address);
        return BucketOf(index);
    }

    internal KBucket BucketOf(int level)
    {
        return _buckets[level];
    }

    internal int GetBucketIndexOf(Address addr)
    {
        int plength = Kademlia.CommonPrefixLength(addr, _address);
        return Math.Min(plength, TableSize - 1);
    }
}
