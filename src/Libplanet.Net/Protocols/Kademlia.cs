using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Random = System.Random;

namespace Libplanet.Net.Protocols;

public sealed class Kademlia
{
    public const int BucketSize = 16;
    public const int TableSize = Address.Size * 8;
    public const int FindConcurrency = 3;
    public const int MaxDepth = 3;

    private readonly TimeSpan _requestTimeout;
    private readonly ITransport _transport;
    private readonly Address _address;
    private readonly Random _random;
    private readonly RoutingTable _table;
    private readonly int _findConcurrency;

    public Kademlia(
        RoutingTable table,
        ITransport transport,
        Address address,
        int findConcurrency = FindConcurrency,
        TimeSpan? requestTimeout = null)
    {
        _transport = transport;

        _address = address;
        _random = new System.Random();
        _findConcurrency = findConcurrency;
        _table = table;
        _requestTimeout = requestTimeout ?? TimeSpan.FromMilliseconds(5000);
        _transport.Process.Subscribe(ProcessMessageHandler);
    }

    public static Address CalculateDifference(Address left, Address right)
    {
        var bytes = new byte[Address.Size];
        var bytes1 = left.Bytes;
        var bytes2 = right.Bytes;

        for (var i = 0; i < Address.Size; i++)
        {
            bytes[i] = (byte)(bytes1[i] ^ bytes2[i]);
        }

        return new Address(bytes);
    }

    public static int CommonPrefixLength(Address left, Address right)
    {
        var bytes = CalculateDifference(left, right).Bytes;
        var length = 0;

        foreach (byte @byte in bytes)
        {
            var mask = 1 << 7;
            while (mask != 0)
            {
                if ((mask & @byte) != 0)
                {
                    return length;
                }

                length++;
                mask >>= 1;
            }
        }

        return length;
    }

    public static int CalculateDistance(Address left, Address right) => (Address.Size * 8)
        - CommonPrefixLength(left, right);

    public static IEnumerable<Peer> SortByDistance(IEnumerable<Peer> peers, Address target)
        => peers.OrderBy(peer => CalculateDistance(target, peer.Address));

    public async Task BootstrapAsync(ImmutableHashSet<Peer> peers, int depth, CancellationToken cancellationToken)
    {
        var findPeerTasks = new List<Task>();
        var history = new ConcurrentBag<Peer>();
        var dialHistory = new ConcurrentBag<Peer>();

        foreach (var peer in peers.Where(peer => !peer.Address.Equals(_address)))
        {
            // Guarantees at least one connection (seed peer)
            try
            {
                await PingAsync(peer, cancellationToken);
                findPeerTasks.Add(
                    FindPeerAsync(
                        history,
                        dialHistory,
                        _address,
                        peer,
                        depth,
                        cancellationToken));
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

        await Task.WhenAll(findPeerTasks).ConfigureAwait(false);
    }

    public Task AddPeersAsync(ImmutableArray<Peer> peers, CancellationToken cancellationToken)
        => Parallel.ForEachAsync(peers, cancellationToken, PingAsync);

    public async Task RefreshTableAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        var peers = _table.PeersToRefresh(maxAge);
        await Parallel.ForEachAsync(peers, cancellationToken, ValidateAsync);
    }

    public Task CheckAllPeersAsync(CancellationToken cancellationToken)
        => Parallel.ForEachAsync(_table.Peers, cancellationToken, ValidateAsync);

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
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
    }

    public async Task CheckReplacementCacheAsync(CancellationToken cancellationToken)
    {
        foreach (IEnumerable<Peer> cache in _table.CachesToCheck)
        {
            foreach (Peer replacement in cache)
            {
                _table.RemoveCache(replacement);
                await PingAsync(replacement, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<Peer?> FindSpecificPeerAsync(Address target, int depth, CancellationToken cancellationToken)
    {
        if (_table.GetPeer(target) is Peer boundPeer)
        {
            try
            {
                await PingAsync(boundPeer, cancellationToken)
                    .ConfigureAwait(false);
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
        foreach (Peer peer in _table.Neighbors(target, _findConcurrency, false))
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
            IEnumerable<Peer> foundPeers =
                await GetNeighbors(viaPeer, target, cancellationToken)
                .ConfigureAwait(false);
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
                    await PingAsync(found, cancellationToken)
                        .ConfigureAwait(false);
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
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await _transport.PingAsync(peer, cancellationToken);
        AddPeer(peer);
    }

    private async void ProcessMessageHandler(MessageEnvelope messageEnvelope)
    {
        switch (messageEnvelope.Message)
        {
            case PingMessage:
                {
                    ReceivePing(messageEnvelope);
                    break;
                }

            case GetPeerMessage:
                {
                    ReceiveFindPeer(messageEnvelope);
                    break;
                }
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
            await PingAsync(peer, cancellationToken).ConfigureAwait(false);
            var endTime = DateTimeOffset.UtcNow;
            _table.Check(peer, startTime, endTime);
        }
        catch
        {
            RemovePeer(peer);
        }
    }

    private void AddPeer(Peer peer)
    {
        _table.Add(peer);
    }

    private void RemovePeer(Peer peer)
    {
        _table.Remove(peer);
    }

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
            found = await QueryNeighborsAsync(history, target, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            found = await GetNeighbors(viaPeer, target, cancellationToken)
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
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IEnumerable<Peer>> QueryNeighborsAsync(
        ConcurrentBag<Peer> history,
        Address target,
        CancellationToken cancellationToken)
    {
        List<Peer> neighbors = _table.Neighbors(target, _table.BucketSize, false).ToList();
        var found = new List<Peer>();
        int count = Math.Min(neighbors.Count, _findConcurrency);
        for (var i = 0; i < count; i++)
        {
            var peers =
                await GetNeighbors(neighbors[i], target, cancellationToken)
                .ConfigureAwait(false);
            history.Add(neighbors[i]);
            found.AddRange(peers.Where(peer => !found.Contains(peer)));
        }

        return found;
    }

    private async Task<IEnumerable<Peer>> GetNeighbors(Peer peer, Address target, CancellationToken cancellationToken)
    {
        var findPeer = new GetPeerMessage { Target = target };
        try
        {
            var replyMessage = await _transport.SendForSingleAsync<PeerMessage>(
                peer, findPeer, cancellationToken);
            return replyMessage.Peers;
        }
        catch (InvalidOperationException cfe)
        {
            RemovePeer(peer);
            return ImmutableArray<Peer>.Empty;
        }
    }

    private void ReceivePing(MessageEnvelope messageEnvelope)
    {
        if (messageEnvelope.Peer.Address.Equals(_address))
        {
            throw new InvalidOperationException("Cannot receive ping from self.");
        }

        var pongMessage = new PongMessage();
        _transport.Reply(messageEnvelope.Identity, pongMessage);
    }

    private async Task ProcessFoundAsync(
        ConcurrentBag<Peer> history,
        ConcurrentBag<Peer> dialHistory,
        IEnumerable<Peer> found,
        Address target,
        int depth,
        CancellationToken cancellationToken)
    {
        List<Peer> peers = found.Where(
            peer =>
                !peer.Address.Equals(_address) &&
                !_table.Contains(peer) &&
                !history.Contains(peer)).ToList();

        if (peers.Count == 0)
        {
            return;
        }

        peers = Kademlia.SortByDistance(peers, target).ToList();

        var closestCandidate = _table.Neighbors(target, _table.BucketSize, false);

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
        await aggregateTask.ConfigureAwait(false);

        var findPeerTasks = new List<Task>();
        Peer? closestKnownPeer = closestCandidate.FirstOrDefault();
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

    private void ReceiveFindPeer(MessageEnvelope messageEnvelope)
    {
        var findNeighbors = (GetPeerMessage)messageEnvelope.Message;
        var found = _table.Neighbors(findNeighbors.Target, _table.BucketSize, true);
        var neighbors = new PeerMessage { Peers = [.. found] };
        _transport.Reply(messageEnvelope.Identity, neighbors);
    }
}
