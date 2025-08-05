using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Subjects;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class MessageIdCollection : IEnumerable<MessageId>
{
    private readonly Subject<ImmutableArray<MessageId>> _addedSubject = new();
    private readonly Subject<ImmutableArray<MessageId>> _removedSubject = new();
    private readonly Subject<Unit> _clearedSubject = new();

    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly HashSet<MessageId> _messageIds = [];

    public IObservable<ImmutableArray<MessageId>> Added => _addedSubject;

    public IObservable<ImmutableArray<MessageId>> Removed => _removedSubject;

    public IObservable<Unit> Cleared => _clearedSubject;

    public int Count
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _messageIds.Count;
        }
    }

    public ImmutableArray<MessageId> Add(ImmutableArray<MessageId> messageIds)
    {
        using var _ = _lock.WriteScope();

        var addedIds = new List<MessageId>(messageIds.Length);
        foreach (var messageId in messageIds)
        {
            if (_messageIds.Add(messageId))
            {
                addedIds.Add(messageId);
            }
        }

        var items = addedIds.ToImmutableArray();
        if (items.Length > 0)
        {
            _addedSubject.OnNext(items);
        }

        return items;
    }

    public ImmutableArray<MessageId> Remove(ImmutableArray<MessageId> messageIds)
    {
        using var _ = _lock.WriteScope();

        var removedIds = new List<MessageId>(messageIds.Length);
        foreach (var messageId in messageIds)
        {
            if (_messageIds.Remove(messageId))
            {
                removedIds.Add(messageId);
            }
        }

        var items = removedIds.ToImmutableArray();
        if (items.Length > 0)
        {
            _removedSubject.OnNext(items);
        }

        return items;
    }

    public void Clear()
    {
        using var _ = _lock.WriteScope();
        _messageIds.Clear();
        _clearedSubject.OnNext(Unit.Default);
    }

    public bool Contains(MessageId messageId)
    {
        using var _ = _lock.ReadScope();
        return _messageIds.Contains(messageId);
    }

    public bool TryGetValue(MessageId messageId, [MaybeNullWhen(false)] out MessageId value)
    {
        using var _ = _lock.ReadScope();
        return _messageIds.TryGetValue(messageId, out value);
    }

    public ImmutableArray<MessageId> Flush(MessageCollection messages)
    {
        using var _ = _lock.WriteScope();
        var items = _messageIds.Where(id => !messages.Contains(id)).ToImmutableArray();
        _messageIds.Clear();
        _clearedSubject.OnNext(Unit.Default);

        return items;
    }

    public IEnumerator<MessageId> GetEnumerator()
    {
        using var _ = _lock.ReadScope();
        var items = _messageIds.ToArray();
        return ((IEnumerable<MessageId>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
