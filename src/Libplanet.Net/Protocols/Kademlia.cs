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
    public const int FindConcurrency = 3;
    public const int MaxDepth = 3;

    private readonly TimeSpan _requestTimeout = TimeSpan.FromMilliseconds(5000);
    private readonly ITransport _transport;
    private readonly Address _address;
    private readonly RoutingTable _table;
    private readonly Bucket _replacementCache = new(256);

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

        foreach (var peer in peers)
        {
            try
            {
                await RefreshPeerAsync(peer, cancellationToken);
                await ExplorePeersAsync(peer.Address, depth, cancellationToken);
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
            .. Enumerable.Range(0, _findConcurrency).Select(_ => GetRandomAddress())
        ];

        foreach (var address in addresses)
        {
            await ExplorePeersAsync(address, depth, cancellationToken);
        }
    }

    public async Task CheckReplacementCacheAsync(CancellationToken cancellationToken)
    {
        var peers = _replacementCache.Select(item => item.Peer).ToArray();
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

                if (count++ >= _findConcurrency)
                {
                    break;
                }

                visited.Add(neighbor);
                queue.Enqueue((neighbor, depth + 1));
            }
        }

        throw new PeerNotFoundException("Failed to find peer.");
    }

    private async Task RefreshPeerAsync(Peer peer, CancellationToken cancellationToken)
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
                var oldestPeerState = _replacementCache.OrderBy(ps => ps.LastUpdated).First();
                var oldestAddress = oldestPeerState.Address;
                _replacementCache.Remove(oldestAddress);
                _replacementCache.AddOrUpdate(peerState);
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

    private async Task ExplorePeersAsync(Address address, int maxDepth, CancellationToken cancellationToken)
    {
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

                await RefreshPeerAsync(neighbor, cancellationToken);

                if (count++ >= _findConcurrency)
                {
                    break;
                }

                visited.Add(neighbor);
                queue.Enqueue((neighbor, depth + 1));
            }
        }

        throw new PeerNotFoundException("Failed to find peer.");
    }
}
