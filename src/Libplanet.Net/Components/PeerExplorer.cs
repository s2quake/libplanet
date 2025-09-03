using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Components.MessageHandlers;
using Libplanet.Types;
using static Libplanet.Net.AddressUtility;

namespace Libplanet.Net.Components;

public sealed class PeerExplorer : IDisposable
{
    public const int FindConcurrency = 3;
    public const int MaxDepth = 3;

    private readonly ITransport _transport;
    private readonly Address _owner;
    private readonly PeerCollection _replacementCache;
    private readonly IDisposable _handlerRegistration;

    public PeerExplorer(ITransport transport, PeerCollection peers)
    {
        if (transport.Peer.Address != peers.Owner)
        {
            throw new ArgumentException(
                "Transport peer address must match the owner of the peer collection.", nameof(peers));
        }

        _owner = transport.Peer.Address;
        _transport = transport;
        Peers = peers;
        _replacementCache = peers.CreateEmpty();
        _handlerRegistration = _transport.MessageRouter.RegisterMany(
        [
            new PingMessageHandler(_transport),
            new PeerRequestMessageHandler(_transport, Peers),
            new DefaultMessageHandler(this),
        ]);
    }

    public Peer Peer => _transport.Peer;

    public ImmutableHashSet<Peer> SeedPeers { get; init; } = [];

    public int MinimumBroadcastTarget { get; set; } = 10;

    public int SearchDepth { get; set; } = 3;

    public PeerCollection Peers { get; }

    public ImmutableArray<Peer> Broadcast(IMessage message) => Broadcast(message, except: default);

    public ImmutableArray<Peer> Broadcast(IMessage message, ImmutableArray<Peer> except)
    {
        var peers = Peers.PeersToBroadcast(except, MinimumBroadcastTarget);
        _transport.Post(peers, message);
        return peers;
    }

    public Task ExploreAsync(CancellationToken cancellationToken)
        => ExploreAsync(SeedPeers, SearchDepth, cancellationToken);

    public Task ExploreAsync(ImmutableHashSet<Peer> seedPeers, int maxDepth, CancellationToken cancellationToken)
        => ExploreInternalAsync(seedPeers, maxDepth, cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken) => RefreshAsync(TimeSpan.Zero, cancellationToken);

    public async Task RefreshAsync(TimeSpan staleThreshold, CancellationToken cancellationToken)
    {
        var peers = Peers.GetStalePeers(staleThreshold);
        var taskList = new List<Task>(peers.Length);
        foreach (var peer in peers)
        {
            var task = PingAsync(peer, cancellationToken);
            taskList.Add(task);
        }

        await Task.WhenAll(taskList);
    }

    public async Task RebuildConnectionAsync(int depth, CancellationToken cancellationToken)
    {
        Address[] addresses =
        [
            _owner,
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
            await PingAsync(peer, cancellationToken);
        }
    }

    public async Task<Peer> FindPeerAsync(Address address, int maxDepth, CancellationToken cancellationToken)
    {
        if (address == _owner)
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

            var neighbors = await GetNeighborsOrDefaultAsync(peer, address, cancellationToken);
            if (neighbors == default)
            {
                continue;
            }

            var count = 0;
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Address == _owner || visited.Contains(neighbor))
                {
                    continue;
                }

                if (await PingAsync(neighbor, cancellationToken) && neighbor.Address == address)
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

    public Task<bool> PingAsync(Peer peer) => PingAsync(peer, cancellationToken: default);

    public async Task<bool> PingAsync(Peer peer, CancellationToken cancellationToken)
    {
        try
        {
            var latency = await _transport.PingAsync(peer, cancellationToken);
            return AddOrUpdate(peer, DateTimeOffset.UtcNow, latency);
        }
        catch (TimeoutException)
        {
            Peers.Remove(peer);
            return false;
        }
    }

    public async Task<ImmutableArray<Peer>> PingManyAsync(
        ImmutableArray<Peer> peers, CancellationToken cancellationToken)
    {
        if (peers.Any(item => item.Address == _owner))
        {
            throw new ArgumentException("Peer list cannot contain self address.", nameof(peers));
        }

        if (peers.Length is 0)
        {
            throw new ArgumentException("Peer list cannot be empty.", nameof(peers));
        }

        var taskList = new List<Task<bool>>(peers.Length);
        foreach (var peer in peers)
        {
            taskList.Add(PingAsync(peer, cancellationToken));
        }

        await Task.WhenAll(taskList);
        return [.. peers.Zip(taskList).Where(item => item.Second.Result).Select(item => item.First)];
    }

    public async Task<ImmutableArray<BlockchainState>> GetBlockchainStateAsync(
        CancellationToken cancellationToken)
    {
        var peers = Peers.ToArray();
        var transport = _transport;
        var taskList = new List<Task<BlockchainState>>(peers.Length);

        foreach (var peer in peers)
        {
            taskList.Add(GetBlockchainStateAsync(peer));

            async Task<BlockchainState> GetBlockchainStateAsync(Peer peer)
            {
                var response = await transport.GetBlockchainStateAsync(peer, cancellationToken);
                return new(peer, response.Genesis, response.Tip);
            }
        }

        try
        {
            await Task.WhenAll(taskList);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // do nothing
        }

        return [.. taskList.Where(item => item.IsCompletedSuccessfully).Select(item => item.Result)];
    }

    public void Dispose()
    {
        _handlerRegistration.Dispose();
    }

    internal bool AddOrUpdate(Peer peer) => AddOrUpdate(peer, DateTimeOffset.UtcNow, TimeSpan.Zero);

    internal bool AddOrUpdate(Peer peer, DateTimeOffset lastUpdated, TimeSpan latency)
    {
        var peerState = Peers.TryGetPeerState(peer, out var v)
            ? v with { LastUpdated = lastUpdated, Latency = latency }
            : new PeerState { Peer = peer, LastUpdated = lastUpdated, Latency = latency };

        if (!Peers.AddOrUpdate(peerState) && !_replacementCache.AddOrUpdate(peerState))
        {
            var bucket = _replacementCache.Buckets[peer.Address];
            var oldestPeerState = bucket.Oldest;
            var oldestAddress = oldestPeerState.Address;
            bucket.Remove(oldestAddress);
            return bucket.AddOrUpdate(peerState);
        }

        return true;
    }

    internal bool Remove(Peer peer)
    {
        if (peer.Address == _owner)
        {
            return false;
        }

        return Peers.Remove(peer) || _replacementCache.Remove(peer);
    }

    private async Task ExploreInternalAsync(ImmutableHashSet<Peer> seedPeers, int maxDepth, CancellationToken cancellationToken)
    {
        if (seedPeers.Any(item => item.Address == _owner))
        {
            throw new ArgumentException("Peer list cannot contain self address.", nameof(seedPeers));
        }

        if (seedPeers.Count is 0)
        {
            throw new ArgumentException("Peer list cannot be empty.", nameof(seedPeers));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var taskList = new List<Task>(seedPeers.Count);
        foreach (var peer in seedPeers)
        {
            taskList.Add(ExplorePeersAsync(peer, _owner, maxDepth, cancellationToken));
        }

        await Task.WhenAll(taskList);
    }

    private async Task ExplorePeersAsync(Peer startingPeer, Address target, int maxDepth, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Peer>();
        var queue = new Queue<(Peer Peer, int Depth)>([(startingPeer, 0)]);
        while (queue.Count > 0)
        {
            var (peer, depth) = queue.Dequeue();
            if (depth > maxDepth)
            {
                continue;
            }

            var neighbors = await GetNeighborsOrDefaultAsync(peer, target, cancellationToken);
            if (neighbors == default)
            {
                continue;
            }

            var count = 0;
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Address == _owner || visited.Contains(neighbor))
                {
                    continue;
                }

                await PingAsync(neighbor, cancellationToken);

                if (count++ >= FindConcurrency)
                {
                    break;
                }

                visited.Add(neighbor);
                queue.Enqueue((neighbor, depth + 1));
            }
        }
    }

    private async Task<ImmutableArray<Peer>> GetNeighborsOrDefaultAsync(
        Peer peer, Address target, CancellationToken cancellationToken)
    {
        if (peer.Address == _owner)
        {
            return Peers.GetNeighbors(target, FindConcurrency, includeTarget: true);
        }

        try
        {
            return await _transport.GetNeighborsAsync(peer, target, cancellationToken);
        }
        catch (TimeoutException)
        {
            return default;
        }
    }
}
