using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Net;

public sealed class BlockDemandCollection(TimeSpan blockDemandLifetime)
    : IReadOnlyDictionary<Peer, BlockDemand>
{
    private readonly ConcurrentDictionary<Peer, BlockDemand> _demandByPeer = new();

    public IEnumerable<Peer> Keys => _demandByPeer.Keys;

    public IEnumerable<BlockDemand> Values => _demandByPeer.Values;

    public int Count => _demandByPeer.Count;

    public BlockDemand this[Peer key] => _demandByPeer[key];

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
        return demand.Timestamp + blockDemandLifetime < DateTimeOffset.UtcNow;
    }

    public bool ContainsKey(Peer key) => _demandByPeer.ContainsKey(key);

    public bool TryGetValue(Peer key, [MaybeNullWhen(false)] out BlockDemand value) => _demandByPeer.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<Peer, BlockDemand>> GetEnumerator() => _demandByPeer.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _demandByPeer.GetEnumerator();
}
