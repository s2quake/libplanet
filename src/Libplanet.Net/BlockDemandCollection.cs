using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class BlockDemandCollection
    : IEnumerable<BlockDemand>
{
    private readonly Subject<BlockDemand> _addedSubject = new();
    private readonly Subject<(BlockDemand, BlockDemand)> _updatedSubject = new();
    private readonly Subject<BlockDemand> _removedSubject = new();
    private readonly Subject<Unit> _clearedSubject = new();

    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly Dictionary<Peer, BlockDemand> _demandByPeer = [];

    public IObservable<BlockDemand> Added => _addedSubject;

    public IObservable<(BlockDemand OldDemand, BlockDemand NewDemand)> Updated => _updatedSubject;

    public IObservable<BlockDemand> Removed => _removedSubject;

    public IObservable<Unit> Cleared => _clearedSubject;

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

    public void Add(BlockDemand demand)
    {
        using var _ = _lock.WriteScope();
        if (!_demandByPeer.TryAdd(demand.Peer, demand))
        {
            throw new ArgumentException("A block demand with the same peer already exists.", nameof(demand));
        }

        _addedSubject.OnNext(demand);
    }

    public void AddOrUpdate(BlockDemand demand)
    {
        using var _ = _lock.WriteScope();
        if (_demandByPeer.TryGetValue(demand.Peer, out var value))
        {
            _demandByPeer[demand.Peer] = demand;
            _updatedSubject.OnNext((value, demand));
        }
        else
        {
            _demandByPeer.Add(demand.Peer, demand);
            _addedSubject.OnNext(demand);
        }
    }

    public bool Remove(Peer peer)
    {
        using var _ = _lock.WriteScope();
        if (_demandByPeer.TryGetValue(peer, out var demand))
        {
            var removed = _demandByPeer.Remove(peer);
            _removedSubject.OnNext(demand);
            return removed;
        }

        return false;
    }

    public void Prune(Blockchain blockchain)
    {
        using var _ = _lock.WriteScope();
        var tipHeight = blockchain.Tip.Height;
        var demands = _demandByPeer.Values.Where(demand => demand.Height <= tipHeight).ToArray();
        foreach (var demand in demands)
        {
            _demandByPeer.Remove(demand.Peer);
            _removedSubject.OnNext(demand);
        }
    }

    public void Clear()
    {
        using var _ = _lock.WriteScope();
        _demandByPeer.Clear();
        _clearedSubject.OnNext(Unit.Default);
    }

    public BlockDemand[] Flush(Blockchain blockchain)
    {
        using var _ = _lock.WriteScope();
        var tipHeight = blockchain.Tip.Height;
        var items = _demandByPeer.Values.Where(item => item.Height > tipHeight).ToArray();
        _demandByPeer.Clear();
        _clearedSubject.OnNext(Unit.Default);
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
