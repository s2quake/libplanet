using Libplanet.Types;

namespace Libplanet.Net;

public static class PeerCollectionExtensions
{
    public static PeerState GetState(this PeerCollection @this, Address address)
        => @this.Buckets[@this.GetBucketIndex(address)][address];

    public static ImmutableArray<Peer> GetStalePeers(this PeerCollection @this, TimeSpan staleThreshold)
    {
        var query = from bucket in @this.Buckets
                    where bucket.Count is not 0 && bucket.Oldest.IsStale(staleThreshold)
                    select bucket.Oldest.Peer;

        return [.. query];
    }

    internal static ImmutableArray<Peer> PeersToBroadcast(
        this PeerCollection @this, ImmutableArray<Peer> except, int minimum = 10)
    {
        var query = from bucket in @this.Buckets
                    where !bucket.IsEmpty
                    let peer = bucket.TryGetRandomPeer(except, out var v) ? v : null
                    where peer is not null
                    select peer;
        var peerList = query.ToList();
        var count = peerList.Count;
        if (count < minimum)
        {
            var rest = @this.Except(peerList)
                .Where(peer => except == default || !except.Contains(peer))
                .Take(minimum - count);
            peerList.AddRange(rest);
        }

        return [.. peerList.Select(item => item)];
    }

    internal static ImmutableArray<Peer> GetNeighbors(
        this PeerCollection @this, Address target, int k, bool includeTarget = false)
    {
        // Select maximum k * 2 peers excluding the target itself.
        var query = from bucket in @this.Buckets
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
}
