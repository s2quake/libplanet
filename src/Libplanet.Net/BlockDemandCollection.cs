using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Net;

public sealed class BlockDemandCollection
    : IEnumerable<BlockDemand>
{
    private readonly ConcurrentDictionary<Peer, BlockDemand> _demandByPeer = new();

    public IEnumerable<Peer> Peers => _demandByPeer.Keys;

    public int Count => _demandByPeer.Count;

    public BlockDemand this[Peer peer] => _demandByPeer[peer];

    public bool AddOrUpdate(BlockDemand blockDemand)
    {
        var value = _demandByPeer.AddOrUpdate(blockDemand.Peer, blockDemand, (_, _) => blockDemand);
        return value == blockDemand;
    }

    public bool Remove(Peer peer) => _demandByPeer.TryRemove(peer, out _);

    public void Prune(Blockchain blockchain)
    {
        var demands = _demandByPeer.Values.ToArray();
        foreach (var demand in demands)
        {
            if (demand.Height <= blockchain.Tip.Height)
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
}
