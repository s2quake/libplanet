using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.PeerServiceMessageHandlers;
using Libplanet.Types;
using Libplanet.Types.Threading;
using static Libplanet.Net.AddressUtility;

namespace Libplanet.Net;

public sealed class PeerService : ServiceBase
{
    public const int FindConcurrency = 3;
    public const int MaxDepth = 3;

    private readonly ITransport _transport;
    private readonly PeerServiceOptions _options;
    private readonly PeerCollection _peers;
    private readonly Address _address;
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
        _options = ValidateOptions(transport.Peer, options);
        _peers = new(_address, options.BucketCount, options.CapacityPerBucket);
        _peers.AddOrUpdateMany([.. options.KnownPeers]);
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

    // public void AddRange(ImmutableHashSet<Peer> peers)
    // {
    //     if (peers.Any(item => item.Address == _address))
    //     {
    //         throw new ArgumentException("Peer list cannot contain self address.", nameof(peers));
    //     }

    //     foreach (var peer in peers)
    //     {
    //         _peers.AddOrUpdate(peer);
    //     }
    // }

    public void Broadcast(IMessage message)
    {
        var peers = _peers.PeersToBroadcast(default);
        _transport.Post(peers, message);
    }

    public void Broadcast(IMessage message, ImmutableArray<Address> except)
    {
        var peers = _peers.PeersToBroadcast(except, _options.MinimumBroadcastTarget);
        _transport.Post(peers, message);
    }

    public Task ExploreAsync(CancellationToken cancellationToken)
        => ExploreAsync(_options.SeedPeers, _options.SearchDepth, cancellationToken);

    public async Task ExploreAsync(ImmutableHashSet<Peer> seedPeers, int maxDepth, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        if (seedPeers.Any(item => item.Address == _address))
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
            try
            {
                await AddOrUpdateAsync(peer, cancellationToken);
                taskList.Add(ExplorePeersAsync(peer, _address, maxDepth, cancellationTokenSource.Token));
            }
            catch (Exception)
            {
                // do nothing
            }
        }

        await Task.WhenAll(taskList);
    }

    public Task RefreshAsync(CancellationToken cancellationToken)
        => RefreshAsync(TimeSpan.Zero, cancellationToken);

    public async Task RefreshAsync(TimeSpan staleThreshold, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        var peers = _peers.GetStalePeers(staleThreshold);
        var taskList = new List<Task>(peers.Length);
        foreach (var peer in peers)
        {
            var task = AddOrUpdateAsync(peer, cancellationTokenSource.Token);
            taskList.Add(task);
        }

        await Task.WhenAll(taskList);
    }

    public async Task RebuildConnectionAsync(int depth, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        Address[] addresses =
        [
            _address,
            .. Enumerable.Range(0, FindConcurrency).Select(_ => GetRandomAddress())
        ];

        foreach (var address in addresses)
        {
            await ExplorePeersAsync(_transport.Peer, address, depth, cancellationTokenSource.Token);
        }
    }

    public async Task CheckReplacementCacheAsync(CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        var query = from bucket in _replacementCache.Buckets
                    from peerState in bucket
                    orderby peerState.LastUpdated
                    select peerState.Peer;
        var peers = query.ToArray();
        foreach (var peer in peers)
        {
            _replacementCache.Remove(peer);
            await AddOrUpdateAsync(peer, cancellationTokenSource.Token);
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

    public async Task<bool> AddOrUpdateAsync(Peer peer, CancellationToken cancellationToken)
    {
        try
        {
            var latency = await _transport.PingAsync(peer, cancellationToken);
            return AddOrUpdate(peer, DateTimeOffset.UtcNow, latency);
        }
        catch (TimeoutException)
        {
            _peers.Remove(peer);
            return false;
        }
    }

    public async Task<ImmutableArray<Peer>> AddOrUpdateManyAsync(
        ImmutableArray<Peer> peers, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);

        if (peers.Any(item => item.Address == _address))
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
            taskList.Add(AddOrUpdateAsync(peer, cancellationTokenSource.Token));
        }

        await Task.WhenAll(taskList);
        return [.. peers.Zip(taskList).Where(item => item.Second.Result).Select(item => item.First)];
    }

    internal bool AddOrUpdate(Peer peer) => AddOrUpdate(peer, DateTimeOffset.UtcNow, TimeSpan.Zero);

    internal bool AddOrUpdate(Peer peer, DateTimeOffset lastUpdated, TimeSpan latency)
    {
        var peerState = _peers.TryGetPeerState(peer, out var v)
                    ? v with { LastUpdated = lastUpdated, Latency = latency }
                    : new PeerState { Peer = peer, LastUpdated = lastUpdated, Latency = latency };

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

    internal bool Remove(Peer peer)
    {
        if (peer.Address == _address)
        {
            return false;
        }

        return _peers.Remove(peer) || _replacementCache.Remove(peer);
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _transport.MessageHandlers.AddRange(_handlers);
        if (_options.SeedPeers.Count > 0)
        {
            await ExploreAsync(_options.SeedPeers, _options.SearchDepth, cancellationToken);
        }
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

    private static PeerServiceOptions ValidateOptions(Peer peer, PeerServiceOptions options)
    {
        ValidationUtility.Validate(options);
        if (options.SeedPeers.Any(item => item.Address == peer.Address))
        {
            throw new ArgumentException(
                "Seed peers cannot contain self address.", nameof(options));
        }

        if (options.KnownPeers.Any(item => item.Address == peer.Address))
        {
            throw new ArgumentException(
                "Known peers cannot contain self address.", nameof(options));
        }

        return options;
    }

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

                await AddOrUpdateAsync(neighbor, cancellationToken);

                if (count++ >= FindConcurrency)
                {
                    break;
                }

                visited.Add(neighbor);
                queue.Enqueue((neighbor, depth + 1));
            }
        }
    }
}
