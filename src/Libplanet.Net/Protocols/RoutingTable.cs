using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;
using static Libplanet.Net.Protocols.AddressUtility;

namespace Libplanet.Net.Protocols;

internal sealed class RoutingTable : IReadOnlyDictionary<Peer, PeerState>
{
    public const int BucketCapacity = 16;
    public const int BucketCount = Address.Size * 8;
    public const int FindConcurrency = 3;
    public const int MaxDepth = 3;

    private readonly Address _owner;
    private readonly Random _random = new();
    // private readonly Kademlia _kademlia;
    private readonly ITransport _transport;
    private readonly int _findConcurrency = FindConcurrency;

    public RoutingTable(
        ITransport transport,
        int bucketCount = BucketCount,
        int bucketCapacity = BucketCapacity)
    {
        _transport = transport;
        _owner = transport.Peer.Address;
        // _kademlia = new Kademlia(this, transport);
        Buckets = new BucketCollection(_owner, bucketCount, bucketCapacity);
    }

    public BucketCollection Buckets { get; }

    public Bucket Pool { get; } = new Bucket(256);

    public IEnumerable<Peer> Keys => Buckets.SelectMany(bucket => bucket.Keys);

    public IEnumerable<PeerState> Values => Buckets.SelectMany(bucket => bucket.Values);

    public int Count => Buckets.Sum(item => item.Count);

    public PeerState this[Peer key] => throw new NotImplementedException();

    public async Task BootstrapAsync(ImmutableHashSet<Peer> peers, int depth, CancellationToken cancellationToken)
    {
        if (peers.Any(item => item.Address == _owner))
        {
            throw new InvalidOperationException($"Cannot bootstrap with self address {_owner} in the peer list.");
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
                    _owner,
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

        // if (!Peers.Any())
        // {
        //     throw new InvalidOperationException("All seeds are unreachable.");
        // }

        // if (findPeerTasks.Count == 0)
        // {
        //     throw new InvalidOperationException("Bootstrap failed.");
        // }
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
                _owner,
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

    public async Task RefreshTableAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        var peers = PeersToRefresh(maxAge);
        await Parallel.ForEachAsync(peers, cancellationToken, ValidateAsync);
    }

    private async ValueTask ValidateAsync(Peer peer, CancellationToken cancellationToken)
    {
        try
        {
            var startTime = DateTimeOffset.UtcNow;
            await PingAsync(peer, cancellationToken);
            var latency = DateTimeOffset.UtcNow - startTime;
            Check(peer, startTime, latency);
        }
        catch
        {
            RemovePeer(peer);
        }
    }

    public async Task CheckReplacementCacheAsync(CancellationToken cancellationToken)
    {
        var query = from peerState in Pool.Values
                    orderby peerState.LastUpdated
                    select peerState.Peer;
        var items = query.ToArray();
        foreach (var item in items)
        {
            Pool.Remove(item);
            await _transport.PingAsync(item, cancellationToken);
            Add(item);
        }
    }

    public async Task AddPeersAsync(ImmutableArray<Peer> peers, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(peers, cancellationToken, PingAsync);
    }

    // public async Task<Peer?> FindSpecificPeerAsync(Address target, int depth, CancellationToken cancellationToken)
    // {
    //     return await _kademlia.FindSpecificPeerAsync(target, depth, cancellationToken);
    // }

    public async Task CheckAllPeersAsync(CancellationToken cancellationToken)
    {
        Parallel.ForEachAsync(Keys, cancellationToken, ValidateAsync);
        // await _kademlia.CheckAllPeersAsync(cancellationToken);
    }

    public bool Add(Peer key) => AddOrUpdate(key, DateTimeOffset.UtcNow);

    public void AddRange(IEnumerable<Peer> peers)
    {
        foreach (var peer in peers)
        {
            Add(peer);
        }
    }

    public bool Remove(Peer peer)
    {
        if (peer.Address != _owner)
        {
            return Buckets[peer].Remove(peer);
        }

        return false;
    }

    public bool ContainsKey(Peer key) => Buckets[key].ContainsKey(key);

    public Peer? GetPeer(Address addr) =>
        Keys.FirstOrDefault(peer => peer.Address.Equals(addr));

    public void Clear()
    {
        foreach (var bucket in Buckets)
        {
            bucket.Clear();
        }
    }

    public ImmutableArray<Peer> GetNeighbors(Address target, int k, bool includeTarget = false)
    {
        // Select maximum k * 2 peers excluding the target itself.
        var query = from bucket in Buckets
                    where !bucket.IsEmpty
                    from peer in bucket.Keys
                    where includeTarget || peer.Address != target
                    orderby AddressUtility.GetDistance(target, peer.Address)
                    select peer;
        var peers = query.ToImmutableArray();
        var containsTarget = peers.Any(peer => peer.Address == target);
        var count = (includeTarget && containsTarget) ? (k * 2) + 1 : k * 2;

        return [.. peers.Take(count)];
    }

    public void Check(Peer peer, DateTimeOffset startTime, TimeSpan latency)
        => Buckets[peer].Check(peer, startTime, latency);

    internal bool AddOrUpdate(Peer peer, DateTimeOffset updated)
    {
        if (peer.Address.Equals(_owner))
        {
            return false;
        }

        return Buckets[peer].AddOrUpdate(peer, updated);
    }

    internal ImmutableArray<Peer> PeersToBroadcast(Address except, int minimum = 10)
    {
        var query = from bucket in Buckets
                    where !bucket.IsEmpty
                    let peer = bucket.TryGetRandomPeer(except, out var v) ? v : null
                    where peer is not null
                    select peer;
        var peerList = query.ToList();
        var count = peerList.Count;
        if (count < minimum)
        {
            var rest = Keys.Except(peerList)
                .Where(peer => peer.Address != except)
                .Take(minimum - count);
            peerList.AddRange(rest);
        }

        return [.. peerList];
    }

    internal ImmutableArray<Peer> PeersToRefresh(TimeSpan maximumAge)
    {
        var dateTimeOffset = DateTimeOffset.UtcNow;
        var query = from bucket in Buckets
                    where !bucket.IsEmpty
                    where bucket.IsEmpty && bucket.Tail.LastUpdated + maximumAge < dateTimeOffset
                    select bucket.Tail.Peer;

        return [.. query];
    }

    public bool TryGetValue(Peer key, [MaybeNullWhen(false)] out PeerState value)
        => Buckets[key].TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<Peer, PeerState>> GetEnumerator()
    {
        foreach (var bucket in Buckets)
        {
            foreach (var pair in bucket)
            {
                yield return pair;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private async void ProcessMessageHandler(MessageEnvelope messageEnvelope)
    {
        switch (messageEnvelope.Message)
        {
            case PingMessage:
                if (messageEnvelope.Peer.Address.Equals(_owner))
                {
                    throw new InvalidOperationException("Cannot receive ping from self.");
                }

                var pongMessage = new PongMessage();
                _transport.Reply(messageEnvelope.Identity, pongMessage);
                break;

            case GetPeerMessage getPeerMessage:
                var target = getPeerMessage.Target;
                var k = Buckets.Count;
                var peers = GetNeighbors(target, k, includeTarget: true);
                var peerMessage = new PeerMessage { Peers = [.. peers] };
                _transport.Reply(messageEnvelope.Identity, peerMessage);
                break;
        }

        // Kademlia protocol registers handle of ITransport with the services
        // (e.g., Swarm, ConsensusReactor) to receive the heartbeat messages.
        // For AsyncDelegate<T> Task.WhenAll(), this will yield the handler
        // to the other services before entering to synchronous AddPeer().
        await Task.Yield();

        Add(messageEnvelope.Peer);
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
        var neighbors = GetNeighbors(target, Buckets.Count);
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
                    where peer.Address != _owner && !ContainsKey(peer) && !history.Contains(peer)
                    orderby GetDistance(target, peer.Address)
                    select peer;
        var peers = query.ToImmutableArray();

        var closestCandidate = GetNeighbors(target, Buckets.Count);

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

    private void RemovePeer(Peer peer) => Remove(peer);

    internal async ValueTask PingAsync(Peer peer, CancellationToken cancellationToken)
    {
        await _transport.PingAsync(peer, cancellationToken);
        Add(peer);
    }

    public async Task<Peer?> FindSpecificPeerAsync(Address target, int depth, CancellationToken cancellationToken)
    {
        if (GetPeer(target) is Peer boundPeer)
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
        foreach (Peer peer in GetNeighbors(target, _findConcurrency))
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
                    !peer.Address.Equals(_owner))
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
}
