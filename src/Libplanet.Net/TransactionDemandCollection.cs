using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class TransactionDemandCollection
    : IEnumerable<TransactionDemand>, INotifyCollectionChanged
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Dictionary<Peer, TransactionDemand> _demandByPeer = [];

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _demandByPeer.Count;
        }
    }

    public TransactionDemand this[Peer peer]
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _demandByPeer[peer];
        }
    }

    public void AddOrUpdate(TransactionDemand demand)
    {
        using var _ = _lock.WriteScope();
        if (_demandByPeer.TryGetValue(demand.Peer, out var value))
        {
            _demandByPeer[demand.Peer] = value with { TxIds = value.TxIds.Union(demand.TxIds) };
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Replace, demand, value));
        }
        else
        {
            _demandByPeer.Add(demand.Peer, demand);
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Add, demand));
        }
    }

    public bool Remove(Peer peer)
    {
        using var _ = _lock.WriteScope();
        if (_demandByPeer.TryGetValue(peer, out var demand))
        {
            var removed = _demandByPeer.Remove(peer);
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Remove, demand));
            return removed;
        }

        return false;
    }

    // public void Prune(Blockchain blockchain)
    // {
    //     using var _ = _lock.WriteScope();
    //     var tipHeight = blockchain.Tip.Height;
    //     var demands = _demandByPeer.Values.Where(demand => demand.Height <= tipHeight).ToArray();
    //     foreach (var demand in demands)
    //     {
    //         _demandByPeer.Remove(demand.Peer);
    //         CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Remove, demand));
    //     }
    // }

    public void Clear()
    {
        using var _ = _lock.WriteScope();
        _demandByPeer.Clear();
        CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
    }

    public TransactionDemand[] Flush()
    {
        using var _ = _lock.WriteScope();
        var items = _demandByPeer.Values.ToArray();
        _demandByPeer.Clear();
        CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
        return items;
    }

    public bool Contains(Peer peer)
    {
        using var _ = _lock.ReadScope();
        return _demandByPeer.ContainsKey(peer);
    }

    public bool TryGetValue(Peer peer, [MaybeNullWhen(false)] out TransactionDemand value)
    {
        using var _ = _lock.ReadScope();
        return _demandByPeer.TryGetValue(peer, out value);
    }

    public IEnumerator<TransactionDemand> GetEnumerator()
    {
        using var _ = _lock.ReadScope();
        var items = _demandByPeer.Values.ToArray();
        return ((IEnumerable<TransactionDemand>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
