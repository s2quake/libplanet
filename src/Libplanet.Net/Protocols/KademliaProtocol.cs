using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Dasync.Collections;
using Libplanet.Net.Messages;
using Libplanet.Net.Transports;
using Libplanet.Types;
using Random = System.Random;

namespace Libplanet.Net.Protocols;

public sealed class KademliaProtocol : IProtocol
{
    private readonly TimeSpan _requestTimeout;
    private readonly ITransport _transport;
    private readonly Address _address;
    private readonly Random _random;
    private readonly RoutingTable _table;
    private readonly int _findConcurrency;

    public KademliaProtocol(
        RoutingTable table,
        ITransport transport,
        Address address,
        int findConcurrency = Kademlia.FindConcurrency,
        TimeSpan? requestTimeout = null)
    {
        _transport = transport;

        _address = address;
        _random = new System.Random();
        _findConcurrency = findConcurrency;
        _table = table;
        _requestTimeout = requestTimeout ?? TimeSpan.FromMilliseconds(5000);
        _transport.ProcessMessageHandler.Register(ProcessMessageHandler);
    }

    public async Task BootstrapAsync(
        IEnumerable<BoundPeer> bootstrapPeers,
        TimeSpan? dialTimeout,
        int depth,
        CancellationToken cancellationToken)
    {
        var findPeerTasks = new List<Task>();
        var history = new ConcurrentBag<BoundPeer>();
        var dialHistory = new ConcurrentBag<BoundPeer>();

        if (!bootstrapPeers.Any())
        {
            throw new PeerDiscoveryException(
                "No seeds are provided.  If it is intended you should conditionally invoke " +
                $"{nameof(BootstrapAsync)}() only when there are seed peers.");
        }

        foreach (BoundPeer peer in bootstrapPeers.Where(peer => !peer.Address.Equals(_address)))
        {
            // Guarantees at least one connection (seed peer)
            try
            {
                await PingAsync(peer, dialTimeout, cancellationToken)
                    .ConfigureAwait(false);
                findPeerTasks.Add(
                    FindPeerAsync(
                        history,
                        dialHistory,
                        _address,
                        peer,
                        depth,
                        dialTimeout,
                        cancellationToken));
            }
            catch (PingTimeoutException)
            {
                RemovePeer(peer);
            }
            catch (Exception e)
            {
            }
        }

        if (!_table.Peers.Any())
        {
            throw new PeerDiscoveryException("All seeds are unreachable.");
        }

        if (findPeerTasks.Count == 0)
        {
            throw new PeerDiscoveryException("Bootstrap failed.");
        }

        try
        {
            await Task.WhenAll(findPeerTasks).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw;
        }
    }

    public async Task AddPeersAsync(
        IEnumerable<BoundPeer> peers,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var tasks = new List<Task>();
            foreach (BoundPeer peer in peers)
            {
                tasks.Add(PingAsync(
                    peer,
                    timeout: timeout,
                    cancellationToken: cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (PingTimeoutException e)
        {
        }
        catch (TaskCanceledException e)
        {
        }
        catch (Exception e)
        {
            throw;
        }
    }

    public async Task RefreshTableAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        // TODO: Add timeout parameter for this method
        try
        {
            IReadOnlyList<BoundPeer> peers = _table.PeersToRefresh(maxAge);

            await peers.ParallelForEachAsync(
                async peer =>
                {
                    try
                    {
                        await ValidateAsync(peer, _requestTimeout, cancellationToken);
                    }
                    catch (TimeoutException)
                    {
                    }
                },
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (TimeoutException)
        {
        }
    }

    public async Task CheckAllPeersAsync(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var peer in _table.Peers)
            {
                await ValidateAsync(peer, timeout ?? _requestTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (TimeoutException e)
        {
        }
    }

    public async Task RebuildConnectionAsync(int depth, CancellationToken cancellationToken)
    {
        var buffer = new byte[20];
        var tasks = new List<Task>();
        var history = new ConcurrentBag<BoundPeer>();
        var dialHistory = new ConcurrentBag<BoundPeer>();
        for (int i = 0; i < _findConcurrency; i++)
        {
            _random.NextBytes(buffer);
            tasks.Add(FindPeerAsync(
                history,
                dialHistory,
                new Address([.. buffer]),
                null,
                depth,
                _requestTimeout,
                cancellationToken));
        }

        tasks.Add(
            FindPeerAsync(
                history,
                dialHistory,
                _address,
                null,
                depth,
                _requestTimeout,
                cancellationToken));
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
    }

    public async Task CheckReplacementCacheAsync(CancellationToken cancellationToken)
    {
        foreach (IEnumerable<BoundPeer> cache in _table.CachesToCheck)
        {
            foreach (BoundPeer replacement in cache)
            {
                try
                {
                    _table.RemoveCache(replacement);
                    await PingAsync(replacement, _requestTimeout, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (PingTimeoutException)
                {
                }
            }
        }
    }

    public async Task<BoundPeer?> FindSpecificPeerAsync(
        Address target,
        int depth,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        if (_table.GetPeer(target) is BoundPeer boundPeer)
        {
            try
            {
                await PingAsync(boundPeer, _requestTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (PingTimeoutException)
            {
                RemovePeer(boundPeer);
                return null;
            }

            return boundPeer;
        }

        HashSet<BoundPeer> history = new HashSet<BoundPeer>();
        Queue<Tuple<BoundPeer, int>> peersToFind = new Queue<Tuple<BoundPeer, int>>();
        foreach (BoundPeer peer in _table.Neighbors(target, _findConcurrency, false))
        {
            peersToFind.Enqueue(new Tuple<BoundPeer, int>(peer, 0));
        }

        while (peersToFind.Any())
        {
            cancellationToken.ThrowIfCancellationRequested();

            peersToFind.Dequeue().Deconstruct(out BoundPeer viaPeer, out int curDepth);
            if (depth != -1 && curDepth >= depth)
            {
                continue;
            }

            history.Add(viaPeer);
            IEnumerable<BoundPeer> foundPeers =
                await GetNeighbors(viaPeer, target, timeout, cancellationToken)
                .ConfigureAwait(false);
            IEnumerable<BoundPeer> filteredPeers = foundPeers
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
                    await PingAsync(found, _requestTimeout, cancellationToken)
                        .ConfigureAwait(false);
                    if (found.Address.Equals(target))
                    {
                        return found;
                    }

                    peersToFind.Enqueue(new Tuple<BoundPeer, int>(found, curDepth + 1));

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
                catch (PingTimeoutException)
                {
                    // Ignore peer not responding
                }
                finally
                {
                    history.Add(found);
                }
            }
        }

        return null;
    }

    internal async Task PingAsync(
        BoundPeer peer,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            Message reply = await _transport.SendMessageAsync(
                peer,
                new PingMessage(),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
            if (!(reply.Content is PongMessage pong))
            {
                throw new InvalidMessageContentException(
                    $"Expected pong, but received {reply.Content.Type}.", reply.Content);
            }
            else if (reply.Remote.Address.Equals(_address))
            {
                throw new InvalidMessageContentException("Cannot receive pong from self", pong);
            }

            AddPeer(peer);
        }
        catch (CommunicationFailException)
        {
            throw new PingTimeoutException(
                $"Failed to send Ping to {peer}.",
                peer);
        }
    }

    private async Task ProcessMessageHandler(Message message)
    {
        switch (message.Content)
        {
            case PingMessage ping:
                {
                    await ReceivePingAsync(message).ConfigureAwait(false);
                    break;
                }

            case FindNeighborsMessage findNeighbors:
                {
                    await ReceiveFindPeerAsync(message).ConfigureAwait(false);
                    break;
                }
        }

        // Kademlia protocol registers handle of ITransport with the services
        // (e.g., Swarm, ConsensusReactor) to receive the heartbeat messages.
        // For AsyncDelegate<T> Task.WhenAll(), this will yield the handler
        // to the other services before entering to synchronous AddPeer().
        await Task.Yield();

        AddPeer(message.Remote);
    }

    private async Task ValidateAsync(
        BoundPeer peer,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DateTimeOffset check = DateTimeOffset.UtcNow;
            await PingAsync(peer, timeout, cancellationToken).ConfigureAwait(false);
            _table.Check(peer, check, DateTimeOffset.UtcNow);
        }
        catch (PingTimeoutException)
        {
            RemovePeer(peer);
            throw new TimeoutException($"Timeout occurred during {nameof(ValidateAsync)}");
        }
    }

    private void AddPeer(BoundPeer peer)
    {
        _table.AddPeer(peer);
    }

    private void RemovePeer(BoundPeer peer)
    {
        _table.RemovePeer(peer);
    }

    private async Task FindPeerAsync(
        ConcurrentBag<BoundPeer> history,
        ConcurrentBag<BoundPeer> dialHistory,
        Address target,
        BoundPeer? viaPeer,
        int depth,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        if (depth == 0)
        {
            return;
        }

        IEnumerable<BoundPeer> found;
        if (viaPeer is null)
        {
            found = await QueryNeighborsAsync(history, target, timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            found = await GetNeighbors(viaPeer, target, timeout, cancellationToken)
                .ConfigureAwait(false);
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
            timeout,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IEnumerable<BoundPeer>> QueryNeighborsAsync(
        ConcurrentBag<BoundPeer> history,
        Address target,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        List<BoundPeer> neighbors = _table.Neighbors(target, _table.BucketSize, false).ToList();
        var found = new List<BoundPeer>();
        int count = Math.Min(neighbors.Count, _findConcurrency);
        for (var i = 0; i < count; i++)
        {
            var peers =
                await GetNeighbors(neighbors[i], target, timeout, cancellationToken)
                .ConfigureAwait(false);
            history.Add(neighbors[i]);
            found.AddRange(peers.Where(peer => !found.Contains(peer)));
        }

        return found;
    }

    private async Task<IEnumerable<BoundPeer>> GetNeighbors(
        BoundPeer peer,
        Address target,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var findPeer = new FindNeighborsMessage { Target = target };
        try
        {
            Message reply = await _transport.SendMessageAsync(
                peer,
                findPeer,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
            if (!(reply.Content is NeighborsMessage neighbors))
            {
                throw new InvalidMessageContentException(
                    $"Reply to {nameof(Messages.FindNeighborsMessage)} is invalid.",
                    reply.Content);
            }

            return neighbors.Found;
        }
        catch (CommunicationFailException cfe)
        {
            RemovePeer(peer);
            return ImmutableArray<BoundPeer>.Empty;
        }
    }

    private async Task ReceivePingAsync(Message message)
    {
        var ping = (PingMessage)message.Content;
        if (message.Remote.Address.Equals(_address))
        {
            throw new InvalidMessageContentException("Cannot receive ping from self.", ping);
        }

        var pong = new PongMessage();

        await _transport.ReplyMessageAsync(pong, message.Identity, default)
            .ConfigureAwait(false);
    }

    private async Task ProcessFoundAsync(
        ConcurrentBag<BoundPeer> history,
        ConcurrentBag<BoundPeer> dialHistory,
        IEnumerable<BoundPeer> found,
        Address target,
        int depth,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        List<BoundPeer> peers = found.Where(
            peer =>
                !peer.Address.Equals(_address) &&
                !_table.Contains(peer) &&
                !history.Contains(peer)).ToList();

        if (peers.Count == 0)
        {
            return;
        }

        peers = Kademlia.SortByDistance(peers, target).ToList();

        IReadOnlyList<BoundPeer> closestCandidate =
            _table.Neighbors(target, _table.BucketSize, false);

        List<Task> tasks = peers
            .Where(peer => !dialHistory.Contains(peer))
            .Select(
                peer =>
                {
                    dialHistory.Add(peer);
                    return PingAsync(peer, _requestTimeout, cancellationToken);
                })
            .ToList();
        Task aggregateTask = Task.WhenAll(tasks);
        try
        {
            await aggregateTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            AggregateException aggregateException = aggregateTask.Exception!;
            foreach (Exception e in aggregateException.InnerExceptions)
            {
                if (e is PingTimeoutException pte)
                {
                    peers.Remove(pte.Target);
                }
            }
        }

        var findPeerTasks = new List<Task>();
        BoundPeer? closestKnownPeer = closestCandidate.FirstOrDefault();
        var count = 0;
        foreach (var peer in peers)
        {
            if (closestKnownPeer is { } ckp &&
               string.CompareOrdinal(
                   Kademlia.CalculateDifference(peer.Address, target).ToString("raw", null),
                   Kademlia.CalculateDifference(ckp.Address, target).ToString("raw", null)) >= 1)
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
                timeout,
                cancellationToken));
            if (count++ >= _findConcurrency)
            {
                break;
            }
        }

        try
        {
            await Task.WhenAll(findPeerTasks).ConfigureAwait(false);
        }
        catch (Exception e)
        {
        }
    }

    private async Task ReceiveFindPeerAsync(Message message)
    {
        var findNeighbors = (FindNeighborsMessage)message.Content;
        IEnumerable<BoundPeer> found =
            _table.Neighbors(findNeighbors.Target, _table.BucketSize, true);

        var neighbors = new NeighborsMessage { Found = [.. found] };

        await _transport.ReplyMessageAsync(neighbors, message.Identity, default)
            .ConfigureAwait(false);
    }
}
