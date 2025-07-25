using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Net;

public sealed class BlockDemandCollection(Blockchain blockchain)
    : IEnumerable<BlockDemand>
{
    private readonly ConcurrentDictionary<Peer, BlockDemand> _demandByPeer = new();

    public TimeSpan BlockDemandLifespan { get; init; } = TimeSpan.FromMinutes(1);

    public IEnumerable<Peer> Peers => _demandByPeer.Keys;

    public int Count => _demandByPeer.Count;

    public BlockDemand this[Peer peer] => _demandByPeer[peer];

    public bool AddOrUpdate(BlockDemand blockDemand)
    {
        if (IsDemandNeeded(blockDemand))
        {
            _demandByPeer[blockDemand.Peer] = blockDemand;
            return true;
        }

        return false;
    }

    public bool Remove(Peer peer) => _demandByPeer.TryRemove(peer, out _);

    public void RemoveAll(Func<BlockSummary, bool> predicate)
    {
        var demands = _demandByPeer.Values.ToArray();
        foreach (var demand in demands)
        {
            if (!predicate(demand.BlockSummary))
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

    private bool IsDemandNeeded(BlockDemand blockDemand)
    {
        if (blockDemand.IsStale(BlockDemandLifespan))
        {
            return false;
        }

        if (blockDemand.Height <= blockchain.Tip.Height)
        {
            return false;
        }

        if (TryGetValue(blockDemand.Peer, out var oldBlockDemand))
        {
            return oldBlockDemand.IsStale(BlockDemandLifespan) || oldBlockDemand.Height < blockDemand.Height;
        }

        return true;
    }
}
