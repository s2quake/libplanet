using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Subjects;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class MessageCollection : IEnumerable<IMessage>
{
    private readonly Subject<IMessage> _addedSubject = new();
    private readonly Subject<IMessage> _removedSubject = new();
    private readonly Subject<Unit> _clearedSubject = new();

    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly Dictionary<MessageId, IMessage> _messageById = [];

    public IObservable<IMessage> Added => _addedSubject;

    public IObservable<IMessage> Removed => _removedSubject;

    public IObservable<Unit> Cleared => _clearedSubject;

    public int Count
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _messageById.Count;
        }
    }

    public IMessage this[MessageId messageId]
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _messageById[messageId];
        }
    }

    public void Add(IMessage message)
    {
        using var _ = _lock.WriteScope();
        if (!_messageById.TryAdd(message.Id, message))
        {
            throw new ArgumentException(
                $"Message with ID {message.Id} already exists in the collection.", nameof(message));
        }
    }

    public bool TryAdd(IMessage message)
    {
        using var _ = _lock.WriteScope();
        if (_messageById.TryAdd(message.Id, message))
        {
            _addedSubject.OnNext(message);
            return true;
        }

        return false;
    }

    public bool Remove(MessageId messageId)
    {
        using var _ = _lock.WriteScope();
        if (_messageById.TryGetValue(messageId, out var message))
        {
            var removed = _messageById.Remove(messageId);
            _removedSubject.OnNext(message);
            return removed;
        }

        return false;
    }

    public void Clear()
    {
        using var _ = _lock.WriteScope();
        _messageById.Clear();
        _clearedSubject.OnNext(Unit.Default);
    }

    public bool Contains(MessageId messageId)
    {
        using var _ = _lock.ReadScope();
        return _messageById.ContainsKey(messageId);
    }

    public bool TryGetValue(MessageId messageId, [MaybeNullWhen(false)] out IMessage value)
    {
        using var _ = _lock.ReadScope();
        return _messageById.TryGetValue(messageId, out value);
    }

    public IEnumerator<IMessage> GetEnumerator()
    {
        using var _ = _lock.ReadScope();
        var items = _messageById.Values.ToArray();
        return ((IEnumerable<IMessage>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal ImmutableArray<MessageId> Except(IEnumerable<MessageId> messageIds)
    {
        using var _ = _lock.ReadScope();
        return [.. _messageById.Keys.Except(messageIds)];
    }

    internal void MoveTo(MessageCollection messages)
    {
        using var _ = _lock.WriteScope();
        foreach (var message in _messageById.Values)
        {
            messages.TryAdd(message);
        }
    }
}
