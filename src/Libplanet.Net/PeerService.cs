using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.PeerServiceMessageHandlers;
using Libplanet.Types;
using static Libplanet.Net.AddressUtility;

namespace Libplanet.Net;

public sealed class PeerService : ServiceBase
{
    public const int FindConcurrency = 3;
    public const int MaxDepth = 3;

    private readonly PeerCollection _peers;
    private readonly Address _address;
    private readonly ITransport _transport;
    private readonly PeerCollection _replacementCache;
    private readonly IMessageHandler[] _handlers;

    public PeerService(ITransport transport)
        : this(transport, PeerServiceOptions.Default)
    {
    }

    public PeerService(ITransport transport, PeerServiceOptions options)
    {
        _address = transport.Peer.Address;
        _transport = transport;
        _peers = new(_address, options.BucketCount, options.CapacityPerBucket);
        _replacementCache = new PeerCollection(_address, options.BucketCount, options.CapacityPerBucket);
        _handlers =
        [
            new PingMessageHandler(_transport),
            new GetPeerMessageHandler(_transport, _peers),
            new DefaultMessageHandler(this),
        ];
    }

    public Peer Peer => _transport.Peer;

    public IPeerCollection Peers => _peers;

    internal bool AddOrUpdate(Peer peer) => AddOrUpdate(peer, DateTimeOffset.UtcNow);

    internal bool AddOrUpdate(Peer peer, DateTimeOffset lastUpdated)
    {
        var peerState = _peers.TryGetPeerState(peer, out var v)
                    ? v with { LastUpdated = lastUpdated }
                    : new PeerState { Peer = peer, LastUpdated = lastUpdated };

        if (!_peers.AddOrUpdate(peerState) && !_replacementCache.AddOrUpdate(peerState))
        {
            var bucket = _replacementCache.Buckets[peer.Address];
            var oldestPeerState = bucket.Oldest;
            var oldestAddress = oldestPeerState.Address;
            bucket.Remove(oldestAddress);
            return bucket.AddOrUpdate(peerState);
        }

        return true;
    }

    public PeerState GetPeerState(Address address) => _peers.GetState(address);

    public void AddRange(ImmutableHashSet<Peer> peers)
    {
        if (peers.Any(item => item.Address == _address))
        {
            throw new ArgumentException("Peer list cannot contain self address.", nameof(peers));
        }

        foreach (var peer in peers)
        {
            _peers.AddOrUpdate(peer);
        }
    }

    public bool Contains(Peer peer) => _peers.Contains(peer);

    public bool Remove(Peer peer)
    {
        if (peer.Address == _address)
        {
            return false;
        }

        return _peers.Remove(peer);
    }

    // public void Clear()
    // {
    //     _peers.Clear();
    //     _replacementCache.Clear();
    // }

    internal ImmutableArray<Peer> PeersToBroadcast(Address except, int minimum = 10)
    {
        var query = from bucket in _peers.Buckets
                    where bucket.Count > 0
                    let peer = bucket.TryGetRandomPeer(except, out var v) ? v : null
                    where peer is not null
                    select peer;
        var peerList = query.ToList();
        var count = peerList.Count;
        if (count < minimum)
        {
            var rest = _peers.Except(peerList)
                .Where(peer => peer.Address != except)
                .Take(minimum - count);
            peerList.AddRange(rest);
        }

        return [.. peerList.Select(item => item)];
    }

    internal ImmutableArray<Peer> GetStalePeers(TimeSpan staleThreshold)
    {
        var query = from bucket in _peers.Buckets
                    where bucket.Count is not 0 && bucket.Oldest.IsStale(staleThreshold)
                    select bucket.Oldest.Peer;

        return [.. query];
    }

    public async Task BootstrapAsync(ImmutableHashSet<Peer> peers, int maxDepth, CancellationToken cancellationToken)
    {
        if (peers.Any(item => item.Address == _address))
        {
            throw new ArgumentException("Peer list cannot contain self address.", nameof(peers));
        }

        if (peers.Count is 0)
        {
            throw new ArgumentException("Peer list cannot be empty.", nameof(peers));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var taskList = new List<Task>(peers.Count);
        foreach (var peer in peers)
        {
            try
            {
                await RefreshPeerAsync(peer, cancellationToken);
                taskList.Add(ExplorePeersAsync(peer, _address, maxDepth, cancellationToken));
            }
            catch (Exception)
            {
                // do nothing
            }
        }

        if (_peers.Count == 0)
        {
            throw new InvalidOperationException("There is no peer in the routing table after bootstrapping.");
        }

        await Task.WhenAll(taskList);
    }

    public async Task RefreshPeersAsync(TimeSpan staleThreshold, CancellationToken cancellationToken)
    {
        var peers = _peers.GetStalePeers(staleThreshold);
        var taskList = new List<Task>(peers.Length);
        foreach (var peer in peers)
        {
            var task = RefreshPeerAsync(peer, cancellationToken);
            taskList.Add(task);
        }

        await Task.WhenAll(taskList);
    }

    public async Task RebuildConnectionAsync(int depth, CancellationToken cancellationToken)
    {
        Address[] addresses =
        [
            _address,
            .. Enumerable.Range(0, FindConcurrency).Select(_ => GetRandomAddress())
        ];

        foreach (var address in addresses)
        {
            await ExplorePeersAsync(_transport.Peer, address, depth, cancellationToken);
        }
    }

    public async Task CheckReplacementCacheAsync(CancellationToken cancellationToken)
    {
        var query = from bucket in _replacementCache.Buckets
                    from peerState in bucket
                    orderby peerState.LastUpdated
                    select peerState.Peer;
        var peers = query.ToArray();
        foreach (var peer in peers)
        {
            _replacementCache.Remove(peer);
            await RefreshPeerAsync(peer, cancellationToken);
        }
    }

    public async Task<Peer> FindPeerAsync(Address address, int maxDepth, CancellationToken cancellationToken)
    {
        if (address == _address)
        {
            throw new ArgumentException("Cannot find self address.", nameof(address));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var visited = new HashSet<Peer>();
        var queue = new Queue<(Peer Peer, int Depth)>([(_transport.Peer, 0)]);
        while (queue.Count > 0)
        {
            var (peer, depth) = queue.Dequeue();
            if (depth > maxDepth)
            {
                continue;
            }

            var neighbors = await _transport.GetNeighborsAsync(peer, address, cancellationToken);
            var count = 0;
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Address == _address || visited.Contains(neighbor))
                {
                    continue;
                }

                if (neighbor.Address == address)
                {
                    return neighbor;
                }

                if (count++ >= FindConcurrency)
                {
                    break;
                }

                visited.Add(neighbor);
                queue.Enqueue((neighbor, depth + 1));
            }
        }

        throw new PeerNotFoundException("Failed to find peer.");
    }

    public async Task RefreshPeerAsync(Peer peer, CancellationToken cancellationToken)
    {
        try
        {
            var latency = await _transport.PingAsync(peer, cancellationToken);
            var peerState = new PeerState
            {
                Peer = peer,
                LastUpdated = DateTimeOffset.UtcNow,
                Latency = latency,
            };

            // if (!_peers.AddOrUpdate(peerState) && !_replacementCache.AddOrUpdate(peerState))
            // {
            //     var bucket = _replacementCache.Buckets[peer.Address];
            //     var oldestPeerState = bucket.Oldest;
            //     var oldestAddress = oldestPeerState.Address;
            //     bucket.Remove(oldestAddress);
            //     bucket.AddOrUpdate(peerState);
            // }
        }
        catch (TimeoutException)
        {
            _peers.Remove(peer);
        }
    }

    public async Task AddPeersAsync(ImmutableArray<Peer> peers, CancellationToken cancellationToken)
    {
        if (peers.Any(item => item.Address == _address))
        {
            throw new ArgumentException("Peer list cannot contain self address.", nameof(peers));
        }

        if (peers.Length is 0)
        {
            throw new ArgumentException("Peer list cannot be empty.", nameof(peers));
        }

        var taskList = new List<Task>(peers.Length);
        foreach (var peer in peers)
        {
            taskList.Add(RefreshPeerAsync(peer, cancellationToken));
        }

        await Task.WhenAll(taskList);
    }

    // public void Dispose()
    // {
    //     if (!_disposed)
    //     {
    //         _transport.MessageHandlers.RemoveRange(_handlers);
    //         _disposed = true;
    //         GC.SuppressFinalize(this);
    //     }
    // }

    private async Task ExplorePeersAsync(Peer viaPeer, Address address, int maxDepth, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Peer>();
        var queue = new Queue<(Peer Peer, int Depth)>([(viaPeer, 0)]);
        while (queue.Count > 0)
        {
            var (peer, depth) = queue.Dequeue();
            if (depth > maxDepth)
            {
                continue;
            }

            var neighbors = peer.Address == _address
                ? _peers.GetNeighbors(address, FindConcurrency, includeTarget: true)
                : await _transport.GetNeighborsAsync(peer, address, cancellationToken);
            var count = 0;
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Address == _address || visited.Contains(neighbor))
                {
                    continue;
                }

                await RefreshPeerAsync(neighbor, cancellationToken);

                if (count++ >= FindConcurrency)
                {
                    break;
                }

                visited.Add(neighbor);
                queue.Enqueue((neighbor, depth + 1));
            }
        }
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _transport.MessageHandlers.AddRange(_handlers);
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _transport.MessageHandlers.RemoveRange(_handlers);
        _peers.Clear();
        _replacementCache.Clear();
        await Task.CompletedTask;
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _transport.MessageHandlers.RemoveRange(_handlers);
        return base.DisposeAsyncCore();
    }
}
