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
    private readonly RoutingTable _routingTable;
    private readonly Bucket _pool = new Bucket(256);

    private readonly int _findConcurrency = FindConcurrency;

    public Kademlia(RoutingTable routingTable, ITransport transport, Address address)
    {
        _transport = transport;
        _address = address;
        _routingTable = routingTable;
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
                await PingAsync(peer, cancellationToken);
                await FindPeerAsync(
                    history,
                    dialHistory,
                    _address,
                    peer,
                    depth,
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                RemovePeer(peer);
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

    public Task AddPeersAsync(ImmutableArray<Peer> peers, CancellationToken cancellationToken)
        => Parallel.ForEachAsync(peers, cancellationToken, PingAsync);

    public async Task RefreshTableAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        var peers = _routingTable.PeersToRefresh(maxAge);
        await Parallel.ForEachAsync(peers, cancellationToken, ValidateAsync);
    }

    public Task CheckAllPeersAsync(CancellationToken cancellationToken)
        => Parallel.ForEachAsync(_routingTable.Keys, cancellationToken, ValidateAsync);

    public async Task RebuildConnectionAsync(int depth, CancellationToken cancellationToken)
    {
        var buffer = new byte[20];
        var tasks = new List<Task>();
        var history = new ConcurrentBag<Peer>();
        var dialHistory = new ConcurrentBag<Peer>();
        for (int i = 0; i < _findConcurrency; i++)
        {
            _random.NextBytes(buffer);
            tasks.Add(FindPeerAsync(
                history,
                dialHistory,
                new Address([.. buffer]),
                null,
                depth,
                cancellationToken));
        }

        tasks.Add(
            FindPeerAsync(
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
        }
    }

    public async Task CheckReplacementCacheAsync(CancellationToken cancellationToken)
    {
        var query = from peerState in _pool.Values
                    orderby peerState.LastUpdated
                    select peerState.Peer;
        var items = query.ToArray();
        foreach (var item in items)
        {
            _pool.Remove(item);
            await PingAsync(item, cancellationToken);
        }
    }

    public async Task<Peer?> FindSpecificPeerAsync(Address target, int depth, CancellationToken cancellationToken)
    {
        if (_routingTable.GetPeer(target) is Peer boundPeer)
        {
            try
            {
                await PingAsync(boundPeer, cancellationToken);
            }
            catch (Exception)
            {
                RemovePeer(boundPeer);
                return null;
            }

            return boundPeer;
        }

        HashSet<Peer> history = new HashSet<Peer>();
        Queue<Tuple<Peer, int>> peersToFind = new Queue<Tuple<Peer, int>>();
        foreach (Peer peer in _routingTable.GetNeighbors(target, _findConcurrency))
        {
            peersToFind.Enqueue(new Tuple<Peer, int>(peer, 0));
        }

        while (peersToFind.Any())
        {
            cancellationToken.ThrowIfCancellationRequested();

            peersToFind.Dequeue().Deconstruct(out Peer viaPeer, out int curDepth);
            if (depth != -1 && curDepth >= depth)
            {
                continue;
            }

            history.Add(viaPeer);
            IEnumerable<Peer> foundPeers = await GetNeighbors(viaPeer, target, cancellationToken);
            IEnumerable<Peer> filteredPeers = foundPeers
                .Where(peer =>
                    !history.Contains(peer) &&
                    !peersToFind.Any(t => t.Item1.Equals(peer)) &&
                    !peer.Address.Equals(_address))
                .Take(_findConcurrency);
            int count = 0;
            foreach (var found in filteredPeers)
            {
                try
                {
                    await PingAsync(found, cancellationToken);
                    if (found.Address.Equals(target))
                    {
                        return found;
                    }

                    peersToFind.Enqueue(new Tuple<Peer, int>(found, curDepth + 1));

                    if (count++ >= _findConcurrency)
                    {
                        break;
                    }
                }
                catch (TaskCanceledException)
                {
                    throw new TaskCanceledException(
                        $"Task is cancelled during {nameof(FindSpecificPeerAsync)}()");
                }
                finally
                {
                    history.Add(found);
                }
            }
        }

        return null;
    }

    internal async ValueTask PingAsync(Peer peer, CancellationToken cancellationToken)
    {
        await _transport.PingAsync(peer, cancellationToken);
        AddPeer(peer);
    }

    private async void ProcessMessageHandler(MessageEnvelope messageEnvelope)
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
                var k = _routingTable.Buckets.Count;
                var peers = _routingTable.GetNeighbors(target, k, includeTarget: true);
                var peerMessage = new PeerMessage { Peers = [.. peers] };
                _transport.Reply(messageEnvelope.Identity, peerMessage);
                break;
        }

        // Kademlia protocol registers handle of ITransport with the services
        // (e.g., Swarm, ConsensusReactor) to receive the heartbeat messages.
        // For AsyncDelegate<T> Task.WhenAll(), this will yield the handler
        // to the other services before entering to synchronous AddPeer().
        await Task.Yield();

        AddPeer(messageEnvelope.Peer);
    }

    private async ValueTask ValidateAsync(Peer peer, CancellationToken cancellationToken)
    {
        try
        {
            var startTime = DateTimeOffset.UtcNow;
            await PingAsync(peer, cancellationToken);
            var latency = DateTimeOffset.UtcNow - startTime;
            _routingTable.Check(peer, startTime, latency);
        }
        catch
        {
            RemovePeer(peer);
        }
    }

    private void AddPeer(Peer peer)
    {
        var updated = DateTimeOffset.UtcNow;
        _routingTable.AddOrUpdate(peer, updated);

        if (!_routingTable.AddOrUpdate(peer, updated) && !_pool.AddOrUpdate(peer, updated))
        {
            var oldestPeer = _pool.OrderBy(ps => ps.Value.LastUpdated).First().Key;
            _pool.Remove(oldestPeer);
            _pool.AddOrUpdate(peer, updated);
        }
    }

    private void RemovePeer(Peer peer) => _routingTable.Remove(peer);

    private async Task FindPeerAsync(
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

        IEnumerable<Peer> found;
        if (viaPeer is null)
        {
            found = await QueryNeighborsAsync(history, target, cancellationToken);
        }
        else
        {
            found = await GetNeighbors(viaPeer, target, cancellationToken);
            history.Add(viaPeer);
        }

        // In ethereum's devp2p, GetNeighbors request will exclude peer with address of
        // target. But our implementation contains target itself for FindSpecificPeerAsync(),
        // so it should be excluded in here.
        found = found.Where(peer => !peer.Address.Equals(target));
        await ProcessFoundAsync(
            history,
            dialHistory,
            found,
            target,
            depth,
            cancellationToken);
    }

    private async Task<IEnumerable<Peer>> QueryNeighborsAsync(
        ConcurrentBag<Peer> history, Address target, CancellationToken cancellationToken)
    {
        var neighbors = _routingTable.GetNeighbors(target, _routingTable.Buckets.Count);
        var foundList = new List<Peer>();
        var count = Math.Min(neighbors.Length, _findConcurrency);
        for (var i = 0; i < count; i++)
        {
            var peers = await GetNeighbors(neighbors[i], target, cancellationToken);
            history.Add(neighbors[i]);
            foundList.AddRange(peers.Where(peer => !foundList.Contains(peer)));
        }

        return foundList;
    }

    private async Task<ImmutableArray<Peer>> GetNeighbors(Peer peer, Address target, CancellationToken cancellationToken)
    {
        var requestMessage = new GetPeerMessage { Target = target };
        try
        {
            var responseMessage = await _transport.SendForSingleAsync<PeerMessage>(
                peer, requestMessage, cancellationToken);
            return responseMessage.Peers;
        }
        catch (InvalidOperationException cfe)
        {
            RemovePeer(peer);
            return [];
        }
    }

    private async Task ProcessFoundAsync(
        ConcurrentBag<Peer> history,
        ConcurrentBag<Peer> dialHistory,
        IEnumerable<Peer> found,
        Address target,
        int depth,
        CancellationToken cancellationToken)
    {
        var query = from peer in found
                    where peer.Address != _address && !_routingTable.ContainsKey(peer) && !history.Contains(peer)
                    orderby GetDistance(target, peer.Address)
                    select peer;
        var peers = query.ToImmutableArray();

        var closestCandidate = _routingTable.GetNeighbors(target, _routingTable.Buckets.Count);

        List<Task> tasks = peers
            .Where(peer => !dialHistory.Contains(peer))
            .Select(
                async peer =>
                {
                    dialHistory.Add(peer);
                    await PingAsync(peer, cancellationToken);
                })
            .ToList();
        Task aggregateTask = Task.WhenAll(tasks);
        await aggregateTask;

        var findPeerTasks = new List<Task>();
        Peer? closestKnownPeer = closestCandidate.FirstOrDefault();
        var count = 0;
        foreach (var peer in peers)
        {
            if (closestKnownPeer is { } ckp &&
               string.CompareOrdinal(
                   GetDifference(peer.Address, target).ToString("raw", null),
                   GetDifference(ckp.Address, target).ToString("raw", null)) >= 1)
            {
                break;
            }

            if (history.Contains(peer))
            {
                continue;
            }

            findPeerTasks.Add(FindPeerAsync(
                history,
                dialHistory,
                target,
                peer,
                depth == -1 ? depth : depth - 1,
                cancellationToken));
            if (count++ >= _findConcurrency)
            {
                break;
            }
        }

        await Task.WhenAll(findPeerTasks);
    }
}
