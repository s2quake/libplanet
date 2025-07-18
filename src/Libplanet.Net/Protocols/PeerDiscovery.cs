using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Protocols.PeerDiscoveryMessageHandlers;
using Libplanet.Types;
using static Libplanet.Net.Protocols.AddressUtility;

namespace Libplanet.Net.Protocols;

internal sealed class PeerDiscovery : IDisposable
{
    public const int FindConcurrency = 3;
    public const int MaxDepth = 3;

    private readonly RoutingTable _table;
    private readonly Address _address;
    private readonly ITransport _transport;
    private readonly RoutingTable _replacementCache;
    private readonly IMessageHandler[] _handlers;
    private bool _disposed;

    public PeerDiscovery(RoutingTable table, ITransport transport)
    {
        if (table.Owner != transport.Peer.Address)
        {
            throw new ArgumentException(
                "The routing table owner must match the transport peer address.",
                nameof(table));
        }

        _table = table;
        _address = table.Owner;
        _transport = transport;
        _replacementCache = new RoutingTable(_address, table.Buckets.Count, table.Buckets.CapacityPerBucket);
        _handlers =
        [
            new PingMessageHandler(_transport),
            new GetPeerMessageHandler(_transport, _table),
            new DefaultMessageHandler(_address, _table, _replacementCache),
        ];
        _transport.MessageHandlers.AddRange(_handlers);
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

        if (!_table.Peers.Any())
        {
            throw new InvalidOperationException("There is no peer in the routing table after bootstrapping.");
        }

        await Task.WhenAll(taskList);
    }

    public async Task RefreshPeersAsync(TimeSpan staleThreshold, CancellationToken cancellationToken)
    {
        var peers = _table.GetStalePeers(staleThreshold);
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

            if (!_table.AddOrUpdate(peerState) && !_replacementCache.AddOrUpdate(peerState))
            {
                var bucket = _replacementCache.Buckets[peer.Address];
                var oldestPeerState = bucket.Oldest;
                var oldestAddress = oldestPeerState.Address;
                bucket.Remove(oldestAddress);
                bucket.AddOrUpdate(peerState);
            }
        }
        catch (TimeoutException)
        {
            _table.Remove(peer);
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

    // private void ProcessMessageHandler(IReplyContext messageEnvelope)
    // {
    //     switch (messageEnvelope.Message)
    //     {
    //         case PingMessage:
    //             if (messageEnvelope.Sender.Address.Equals(_address))
    //             {
    //                 throw new InvalidOperationException("Cannot receive ping from self.");
    //             }

    //             messageEnvelope.PongAsync();
    //             break;

    //         case GetPeerMessage getPeerMessage:
    //             if (messageEnvelope.Sender.Address.Equals(_address))
    //             {
    //                 throw new InvalidOperationException("Cannot receive ping from self.");
    //             }

    //             var target = getPeerMessage.Target;
    //             var k = RoutingTable.BucketCount;
    //             var peers = _table.GetNeighbors(target, k, includeTarget: true);
    //             var peerMessage = new PeerMessage { Peers = [.. peers] };
    //             messageEnvelope.NextAsync(peerMessage);
    //             break;
    //     }

    //     if (messageEnvelope.Sender.Address != _address)
    //     {
    //         var peer = messageEnvelope.Sender;
    //         var peerState = _table.TryGetValue(peer.Address, out var v)
    //             ? v with { LastUpdated = DateTimeOffset.UtcNow }
    //             : new PeerState { Peer = peer, LastUpdated = DateTimeOffset.UtcNow };

    //         if (!_table.AddOrUpdate(peerState) && !_replacementCache.AddOrUpdate(peerState))
    //         {
    //             var bucket = _replacementCache.Buckets[peer.Address];
    //             var oldestPeerState = bucket.Oldest;
    //             var oldestAddress = oldestPeerState.Address;
    //             bucket.Remove(oldestAddress);
    //             bucket.AddOrUpdate(peerState);
    //         }
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
                ? _table.GetNeighbors(address, FindConcurrency, includeTarget: true)
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _transport.MessageHandlers.RemoveRange(_handlers);
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
