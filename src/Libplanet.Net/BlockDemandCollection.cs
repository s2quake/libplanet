using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Net;

public sealed class BlockDemandCollection(TimeSpan blockDemandLifetime)
    : IEnumerable<BlockDemand>
{
    private readonly ConcurrentDictionary<Peer, BlockDemand> _demandByPeer = new();

    public IEnumerable<Peer> Peers => _demandByPeer.Keys;

    public int Count => _demandByPeer.Count;

    public BlockDemand this[Peer peer] => _demandByPeer[peer];

    public void Add(Func<BlockSummary, bool> predicate, BlockDemand demand)
    {
        if (IsDemandNeeded(predicate, demand))
        {
            _demandByPeer[demand.Peer] = demand;
        }
    }

    public bool Remove(Peer peer) => _demandByPeer.TryRemove(peer, out _);

    public void Cleanup(Func<BlockSummary, bool> predicate)
    {
        foreach (var demand in _demandByPeer.Values)
        {
            if (!predicate(demand.BlockSummary) || IsDemandStale(demand))
            {
                _demandByPeer.TryRemove(demand.Peer, out _);
            }
        }
    }

    public void Clear() => _demandByPeer.Clear();

    public bool Contains(Peer peer) => _demandByPeer.ContainsKey(peer);

    public bool TryGetValue(Peer peer, [MaybeNullWhen(false)] out BlockDemand value)
        => _demandByPeer.TryGetValue(peer, out value);

    public IEnumerator<BlockDemand> GetEnumerator() => _demandByPeer.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private bool IsDemandNeeded(Func<BlockSummary, bool> predicate, BlockDemand demand)
    {
        BlockDemand? oldDemand = _demandByPeer.ContainsKey(demand.Peer)
            ? _demandByPeer[demand.Peer]
            : (BlockDemand?)null;

        bool needed;
        if (IsDemandStale(demand))
        {
            needed = false;
        }
        else if (predicate(demand.BlockSummary))
        {
            if (oldDemand is { } old)
            {
                needed = IsDemandStale(old) || old.Height < demand.Height;
            }
            else
            {
                needed = true;
            }
        }
        else
        {
            needed = false;
        }

        return needed;
    }

    private bool IsDemandStale(BlockDemand demand)
    {
        return demand.IsStale(blockDemandLifetime);
    }
}
