using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class BlockDemandCollection
    : IEnumerable<BlockDemand>, INotifyCollectionChanged
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Dictionary<Peer, BlockDemand> _demandByPeer = [];

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _demandByPeer.Count;
        }
    }

    public BlockDemand this[Peer peer]
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _demandByPeer[peer];
        }
    }

    public void AddOrUpdate(BlockDemand blockDemand)
    {
        using var _ = _lock.WriteScope();
        if (_demandByPeer.TryGetValue(blockDemand.Peer, out var value))
        {
            _demandByPeer[blockDemand.Peer] = blockDemand;
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Replace, blockDemand, value));
        }
        else
        {
            _demandByPeer.Add(blockDemand.Peer, blockDemand);
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Add, blockDemand));
        }
    }

    public bool Remove(Peer peer)
    {
        using var _ = _lock.WriteScope();
        if (_demandByPeer.TryGetValue(peer, out var blockDemand))
        {
            var removed = _demandByPeer.Remove(peer);
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Remove, blockDemand));
            return removed;
        }

        return false;
    }

    public void Prune(Blockchain blockchain)
    {
        using var _ = _lock.WriteScope();
        var tipHeight = blockchain.Tip.Height;
        var blockDemands = _demandByPeer.Values.Where(demand => demand.Height <= tipHeight).ToArray();
        foreach (var blockDemand in blockDemands)
        {
            _demandByPeer.Remove(blockDemand.Peer);
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Remove, blockDemand));
        }
    }

    public void Clear()
    {
        using var _ = _lock.WriteScope();
        _demandByPeer.Clear();
        CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
    }

    public BlockDemand[] Flush(Blockchain blockchain)
    {
        using var _ = _lock.WriteScope();
        var tipHeight = blockchain.Tip.Height;
        var items = _demandByPeer.Values.Where(item => item.Height > tipHeight).ToArray();
        _demandByPeer.Clear();
        CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
        return items;
    }

    public bool Contains(Peer peer)
    {
        using var _ = _lock.ReadScope();
        return _demandByPeer.ContainsKey(peer);
    }

    public bool TryGetValue(Peer peer, [MaybeNullWhen(false)] out BlockDemand value)
    {
        using var _ = _lock.ReadScope();
        return _demandByPeer.TryGetValue(peer, out value);
    }

    public IEnumerator<BlockDemand> GetEnumerator()
    {
        using var _ = _lock.ReadScope();
        var items = _demandByPeer.Values.ToArray();
        return ((IEnumerable<BlockDemand>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
