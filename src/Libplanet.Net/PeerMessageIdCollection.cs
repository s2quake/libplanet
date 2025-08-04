using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Subjects;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class PeerMessageIdCollection
    : IEnumerable<KeyValuePair<Peer, MessageIdCollection>>
{
    private readonly Subject<(Peer, ImmutableArray<MessageId>)> _addedSubject = new();
    private readonly Subject<(Peer, ImmutableArray<MessageId>)> _removedSubject = new();
    private readonly Subject<Unit> _clearedSubject = new();

    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly Dictionary<Peer, MessageIdCollection> _itemsByPeer = [];

    public IObservable<(Peer, ImmutableArray<MessageId>)> Added => _addedSubject;

    public IObservable<(Peer, ImmutableArray<MessageId>)> Removed => _removedSubject;

    public IObservable<Unit> Cleared => _clearedSubject;

    public int Count
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _itemsByPeer.Count;
        }
    }

    public MessageIdCollection this[Peer peer]
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _itemsByPeer[peer];
        }
    }

    public ImmutableArray<MessageId> Add(Peer peer, ImmutableArray<MessageId> messageIds)
    {
        using var _ = _lock.WriteScope();
        if (!_itemsByPeer.TryGetValue(peer, out var items))
        {
            items = [];
            _itemsByPeer.Add(peer, items);
        }

        var added = items.Add(messageIds);
        if (added.Length > 0)
        {
            _addedSubject.OnNext((peer, added));
        }

        _addedSubject.OnNext((peer, added));
        return added;
    }

    public ImmutableArray<MessageId> Remove(Peer peer, ImmutableArray<MessageId> messageIds)
    {
        using var _ = _lock.WriteScope();

        if (!_itemsByPeer.TryGetValue(peer, out var items))
        {
            return [];
        }

        var removed = items.Remove(messageIds);
        if (removed.Length > 0)
        {
            _removedSubject.OnNext((peer, removed));
        }

        return removed;
    }

    public void Clear()
    {
        using var _ = _lock.WriteScope();
        _itemsByPeer.Clear();
        _clearedSubject.OnNext(Unit.Default);
    }

    public bool Contains(Peer peer)
    {
        using var _ = _lock.ReadScope();
        return _itemsByPeer.ContainsKey(peer);
    }

    public bool TryGetValue(Peer peer, [MaybeNullWhen(false)] out MessageIdCollection value)
    {
        using var _ = _lock.ReadScope();
        return _itemsByPeer.TryGetValue(peer, out value);
    }

    public ImmutableArray<(Peer, ImmutableArray<MessageId>)> Flush(MessageCollection messages)
    {
        using var _ = _lock.WriteScope();
        var query = from kv in _itemsByPeer
                    let ids = kv.Value.Flush(messages)
                    where ids.Length > 0
                    select (kv.Key, ids);

        return [.. query];
    }

    public IEnumerator<KeyValuePair<Peer, MessageIdCollection>> GetEnumerator()
    {
        using var _ = _lock.ReadScope();
        var items = _itemsByPeer.ToArray();
        return ((IEnumerable<KeyValuePair<Peer, MessageIdCollection>>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
