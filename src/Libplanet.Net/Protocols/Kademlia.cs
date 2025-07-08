using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Random = System.Random;
using static Libplanet.Net.Protocols.AddressUtility;

namespace Libplanet.Net.Protocols;

internal sealed class Kademlia
{
    public const int BucketCapacity = 16;
    public const int BucketCount = Address.Size * 8;
    public const int FindConcurrency = 3;
    public const int MaxDepth = 3;

    private readonly TimeSpan _requestTimeout = TimeSpan.FromMilliseconds(5000);
    private readonly ITransport _transport;
    private readonly Address _address;
    private readonly Random _random = new();
    private readonly RoutingTable _table;
    private readonly Bucket _pool = new(256);

    private readonly int _findConcurrency = FindConcurrency;

    public Kademlia(RoutingTable table, ITransport transport, Address address)
    {
        _transport = transport;
        _address = address;
        _table = table;
        _transport.Process.Subscribe(ProcessMessageHandler);
    }

    public async Task BootstrapAsync(ImmutableHashSet<Peer> peers, int depth, CancellationToken cancellationToken)
    {
        if (peers.Any(item => item.Address == _address))
        {
            throw new InvalidOperationException($"Cannot bootstrap with self address {_address} in the peer list.");
        }

        var visited = new ConcurrentBag<Peer>();

        foreach (var peer in peers)
        {
            // Guarantees at least one connection (seed peer)
            try
            {
                await RefreshAsync(peer, cancellationToken);
                await ExplorePeersAsync(
                    visited,
                    _address,
                    peer,
                    depth,
                    cancellationToken);
            }
            catch (Exception)
            {
                // do nothing
            }
        }

        if (!_table.Peers.Any())
        {
            throw new InvalidOperationException("All seeds are unreachable.");
        }

        // if (findPeerTasks.Count == 0)
        // {
        //     throw new InvalidOperationException("Bootstrap failed.");
        // }
    }

    public async Task RefreshAsync(TimeSpan staleThreshold, CancellationToken cancellationToken)
    {
        var peers = _table.PeersToRefresh(staleThreshold);
        var taskList = new List<Task>(peers.Length);
        foreach (var peer in peers)
        {
            var task = RefreshAsync(peer, cancellationToken);
            taskList.Add(task);
        }

        await Task.WhenAll(taskList);
    }

    public async Task RebuildConnectionAsync(int depth, CancellationToken cancellationToken)
    {
        var buffer = new byte[20];
        var taskList = new List<Task>();
        var history = new ConcurrentBag<Peer>();
        var addressList = new List<Address>
        {
            _address,
        };
        for (var i = 0; i < _findConcurrency; i++)
        {
            _random.NextBytes(buffer);
            addressList.Add(new Address([.. buffer]));
        }

        foreach (var address in addressList)
        {
            var task = ExplorePeersAsync(
                history,
                address,
                null,
                depth,
                cancellationToken);
            taskList.Add(task);
        }

        await Task.WhenAll(taskList);
    }

    public async Task CheckReplacementCacheAsync(CancellationToken cancellationToken)
    {
        var query = from peerState in _pool
                    orderby peerState.LastUpdated
                    select peerState.Peer;
        var peers = query.ToArray();
        foreach (var peer in peers)
        {
            _pool.Remove(peer);
            await RefreshAsync(peer, cancellationToken);
        }
    }

    public async Task<Peer> FindPeerAsync(Address address, int depth, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depth);

        if (_table.TryGetPeer(address, out var value))
        {
            return value;
        }

        var visited = new HashSet<Peer>();
        var queue = new Queue<(Peer Peer, int Depth)>();
        var localPeers = _table.GetNeighbors(address, _findConcurrency);
        foreach (var peer in localPeers)
        {
            queue.Enqueue((peer, 1));
            visited.Add(peer);
        }

        while (queue.Count > 0)
        {
            var (currentPeer, currentDepth) = queue.Dequeue();
            if (currentDepth > depth)
            {
                continue;
            }

            var neighbors = await _transport.GetNeighborsAsync(currentPeer, address, cancellationToken);
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

                if (count++ >= _findConcurrency)
                {
                    break;
                }

                visited.Add(neighbor);
                queue.Enqueue((neighbor, currentDepth + 1));
            }
        }

        throw new PeerNotFoundException("Failed to find peer.");
    }

    private async Task RefreshAsync(Peer peer, CancellationToken cancellationToken)
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

            if (!_table.AddOrUpdate(peerState) && !_pool.AddOrUpdate(peerState))
            {
                var oldestPeerState = _pool.OrderBy(ps => ps.LastUpdated).First();
                var oldestAddress = oldestPeerState.Address;
                _pool.Remove(oldestAddress);
                _pool.AddOrUpdate(peerState);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            _table.Remove(peer);
            throw;
        }
    }

    private void ProcessMessageHandler(MessageEnvelope messageEnvelope)
    {
        switch (messageEnvelope.Message)
        {
            case PingMessage:
                if (messageEnvelope.Peer.Address.Equals(_address))
                {
                    throw new InvalidOperationException("Cannot receive ping from self.");
                }

                var pongMessage = new PongMessage();
                _transport.Reply(messageEnvelope.Identity, pongMessage);
                break;

            case GetPeerMessage getPeerMessage:
                var target = getPeerMessage.Target;
                var k = _table.Buckets.Count;
                var peers = _table.GetNeighbors(target, k, includeTarget: true);
                var peerMessage = new PeerMessage { Peers = [.. peers] };
                _transport.Reply(messageEnvelope.Identity, peerMessage);
                break;
        }
    }

    private async Task ExplorePeersAsync(
        ConcurrentBag<Peer> visited,
        Address target,
        Peer? viaPeer,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth == 0)
        {
            return;
        }

        var k = _table.Buckets.Count;
        var localNeighbors = viaPeer is null ? _table.GetNeighbors(target, k) : [viaPeer];
        var neighborList = new List<Peer>();
        var count = Math.Min(localNeighbors.Length, _findConcurrency);
        for (var i = 0; i < count; i++)
        {
            var localNeighbor = localNeighbors[i];
            var remoteNeighbors = await _transport.GetNeighborsAsync(localNeighbor, target, cancellationToken);
            visited.Add(localNeighbor);
            neighborList.AddRange(remoteNeighbors);
        }

        neighborList.RemoveAll(peer => peer.Address == _address);

        var query = from peer in neighborList
                    where peer.Address != _address && !_table.Contains(peer) && !visited.Contains(peer)
                    orderby GetDistance(target, peer.Address)
                    select peer;
        var peers = query.ToImmutableArray();
        var closestNeighbors = _table.GetNeighbors(target, k);
        // var tasks = peers
        //     // .Where(peer => !dialHistory.Contains(peer))
        //     .Select(
        //         async peer =>
        //         {
        //             dialHistory.Add(peer);
        //             await RefreshAsync(peer, cancellationToken);
        //         })
        //     .ToArray();
        // await Task.WhenAll(tasks);

        var findTaskList = new List<Task>();
        Peer? closestPeer = closestNeighbors.FirstOrDefault();
        count = 0;
        foreach (var peer in peers)
        {
            if (closestPeer is not null
                && GetDistance(peer.Address, target) >= GetDistance(closestPeer.Address, target))
            {
                break;
            }

            if (visited.Contains(peer))
            {
                continue;
            }

            var findTask = ExplorePeersAsync(
                visited,
                target,
                peer,
                depth == -1 ? depth : depth - 1,
                cancellationToken);
            findTaskList.Add(findTask);
            if (count++ >= _findConcurrency)
            {
                break;
            }
        }

        await Task.WhenAll(findTaskList);
    }
}
