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

        var history = new ConcurrentBag<Peer>();
        var dialHistory = new ConcurrentBag<Peer>();

        foreach (var peer in peers)
        {
            // Guarantees at least one connection (seed peer)
            try
            {
                await TryRefreshAsync(peer, cancellationToken);
                await UpdatePeersAsync(
                    history,
                    dialHistory,
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

        // if (!_table.Peers.Any())
        // {
        //     throw new InvalidOperationException("All seeds are unreachable.");
        // }

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
            var task = TryRefreshAsync(peer, cancellationToken);
            taskList.Add(task);
        }

        await Task.WhenAll(taskList);
    }

    public async Task RebuildConnectionAsync(int depth, CancellationToken cancellationToken)
    {
        var buffer = new byte[20];
        var tasks = new List<Task>();
        var history = new ConcurrentBag<Peer>();
        var dialHistory = new ConcurrentBag<Peer>();
        for (int i = 0; i < _findConcurrency; i++)
        {
            _random.NextBytes(buffer);
            tasks.Add(UpdatePeersAsync(
                history,
                dialHistory,
                new Address([.. buffer]),
                null,
                depth,
                cancellationToken));
        }

        tasks.Add(
            UpdatePeersAsync(
                history,
                dialHistory,
                _address,
                null,
                depth,
                cancellationToken));
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (TimeoutException)
        {
            // do nothing
        }
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
            await TryRefreshAsync(peer, cancellationToken);
        }
    }

    public async Task<Peer?> FindPeerAsync(Address target, int depth, CancellationToken cancellationToken)
    {
        if (_table.TryGetPeer(target, out var peer) && await TryRefreshAsync(peer, cancellationToken))
        {
            return peer;
        }

        var history = new HashSet<Peer>();
        var peersToFind = new Queue<(Peer Peer, int Depth)>();
        var localNeighbors = _table.GetNeighbors(target, _findConcurrency);
        foreach (var localNeighbor in localNeighbors)
        {
            peersToFind.Enqueue((localNeighbor, 0));
        }

        while (peersToFind.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (viaPeer, curDepth) = peersToFind.Dequeue();
            if (depth != -1 && curDepth >= depth)
            {
                continue;
            }

            history.Add(viaPeer);
            var remoteNeighbors = await _transport.GetNeighborsAsync(viaPeer, target, cancellationToken);
            IEnumerable<Peer> filteredPeers = remoteNeighbors
                .Where(peer =>
                    !history.Contains(peer) &&
                    !peersToFind.Any(item => item.Peer == peer) &&
                    peer.Address != _address)
                .Take(_findConcurrency);
            var count = 0;
            foreach (var found in filteredPeers)
            {
                try
                {
                    await TryRefreshAsync(found, cancellationToken);
                    if (found.Address == target)
                    {
                        return found;
                    }

                    peersToFind.Enqueue((found, curDepth + 1));

                    if (count++ >= _findConcurrency)
                    {
                        break;
                    }
                }
                catch (TaskCanceledException)
                {
                    throw new TaskCanceledException(
                        $"Task is cancelled during {nameof(FindPeerAsync)}()");
                }
                finally
                {
                    history.Add(found);
                }
            }
        }

        return null;
    }

    private async Task<bool> TryRefreshAsync(Peer peer, CancellationToken cancellationToken)
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

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            _table.Remove(peer);
            return false;
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

    private async Task UpdatePeersAsync(
        ConcurrentBag<Peer> history,
        ConcurrentBag<Peer> dialHistory,
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
            history.Add(localNeighbor);
            neighborList.AddRange(remoteNeighbors);
        }

        neighborList.RemoveAll(peer => peer.Address == _address);

        var query = from peer in neighborList
                    where peer.Address != _address && !_table.Contains(peer) && !history.Contains(peer)
                    orderby GetDistance(target, peer.Address)
                    select peer;
        var peers = query.ToImmutableArray();

        // var k = _table.Buckets.Count;
        var closestCandidate = _table.GetNeighbors(target, k);

        var tasks = peers
            .Where(peer => !dialHistory.Contains(peer))
            .Select(
                async peer =>
                {
                    dialHistory.Add(peer);
                    await TryRefreshAsync(peer, cancellationToken);
                })
            .ToArray();
        await Task.WhenAll(tasks);

        var findTaskList = new List<Task>();
        Peer? closestPeer = closestCandidate.FirstOrDefault();
        count = 0;
        foreach (var peer in peers)
        {
            if (closestPeer is not null
                && GetDistance(peer.Address, target) >= GetDistance(closestPeer.Address, target))
            {
                break;
            }

            if (history.Contains(peer))
            {
                continue;
            }

            var findTask = UpdatePeersAsync(
                history,
                dialHistory,
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
