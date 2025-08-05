using System.Collections;
using System.Diagnostics;
using System.Reactive.Subjects;
using Libplanet.Net.Messages;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class MessageRouter(ProtocolHash protocolHash)
    : IEnumerable<IMessageHandler>, IAsyncDisposable, IMessageRouter
{
    private readonly Subject<(IMessageHandler, Exception)> _errorOccurredSubject = new();
    private readonly Subject<MessageEnvelope> _invalidProtocolSubject = new();
    private readonly Dictionary<Type, List<IMessageHandler>> _handlersByType = [];
    private readonly List<IMessageHandler> _handlerList = [];
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public IObservable<(IMessageHandler, Exception)> ErrorOccurred => _errorOccurredSubject;

    public IObservable<MessageEnvelope> InvalidProtocol => _invalidProtocolSubject;

    public int Count
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _handlerList.Count;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _errorOccurredSubject.Dispose();
        foreach (var handler in _handlerList)
        {
            if (handler is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }

            if (handler is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public IDisposable Register(IMessageHandler handler)
    {
        using var _ = _lock.WriteScope();
        AddInternal(handler);
        return new Unregister(this, [handler]);
    }

    public IDisposable Register<T>(Action<T, MessageEnvelope> action)
        where T : IMessage
    {
        var handler = new RelayMessageHandler<T>(action);
        AddInternal(handler);
        return new Unregister(this, [handler]);
    }

    public IDisposable Register<T>(Action<T> action)
        where T : IMessage
    {
        var handler = new RelayMessageHandler<T>(action);
        AddInternal(handler);
        return new Unregister(this, [handler]);
    }

    public IDisposable RegisterMany(ImmutableArray<IMessageHandler> handlers)
    {
        using var _ = _lock.WriteScope();
        foreach (var handler in handlers)
        {
            AddInternal(handler);
        }

        return new Unregister(this, handlers);
    }

    public void UnregisterMany(IEnumerable<IMessageHandler> handlers)
    {
        using var _ = _lock.WriteScope();
        foreach (var handler in handlers)
        {
            RemoveInternal(handler);
        }
    }

    public bool Contains(Type messageType)
    {
        using var _ = new ReadScope(_lock);
        return _handlersByType.ContainsKey(messageType);
    }

    public async Task HandleAsync(MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        if (messageEnvelope.ProtocolHash != protocolHash)
        {
            _invalidProtocolSubject.OnNext(messageEnvelope);
            return;
        }

        var handlerList = new List<IMessageHandler>();
        using (new ReadScope(_lock))
        {
            var message = messageEnvelope.Message;
            var messageType = message.GetType();
            while (messageType is not null && typeof(IMessage).IsAssignableFrom(messageType))
            {
                if (_handlersByType.TryGetValue(messageType, out var handlers1))
                {
                    handlerList.AddRange(handlers1);
                }

                messageType = messageType.BaseType;
            }

            if (_handlersByType.TryGetValue(typeof(IMessage), out var handlers2))
            {
                handlerList.AddRange(handlers2);
            }
        }

        await Parallel.ForEachAsync(handlerList, cancellationToken, async (handler, cancellationToken) =>
        {
            try
            {
                await handler.HandleAsync(messageEnvelope, cancellationToken);
            }
            catch (Exception e)
            {
                _errorOccurredSubject.OnNext((handler, e));
            }
        });
    }

    public IEnumerator<IMessageHandler> GetEnumerator()
    {
        foreach (var item in _handlerList.ToArray())
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void AddInternal(IMessageHandler handler)
    {
        if (!_handlersByType.TryGetValue(handler.MessageType, out var handlers1))
        {
            handlers1 = [];
        }

        handlers1.Add(handler);
        _handlersByType[handler.MessageType] = handlers1;
        _handlerList.Add(handler);
    }

    private void RemoveInternal(IMessageHandler handler)
    {
        if (!_handlersByType.TryGetValue(handler.MessageType, out var value))
        {
            return;
        }

        var removed = value.Remove(handler);
        if (removed && !_handlerList.Remove(handler))
        {
            throw new UnreachableException("Failed to remove handler from the list.");
        }

        if (value.Count == 0)
        {
            _handlersByType.Remove(handler.MessageType);
        }
    }

    private sealed class Unregister(MessageRouter router, ImmutableArray<IMessageHandler> handlers)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                router.UnregisterMany(handlers);
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
