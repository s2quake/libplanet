using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net;

public sealed class MessageHandlerCollection(IEnumerable<IMessageHandler> handlers)
    : IEnumerable<IMessageHandler>, IDisposable
{
    private readonly Dictionary<Type, IMessageHandler> _handlerByType = handlers.ToDictionary(item => item.MessageType);

    public MessageHandlerCollection()
        : this([])
    {
    }

    public int Count => _handlerByType.Count;

    public IMessageHandler this[Type messageType] => _handlerByType[messageType];

    public void Add(IMessageHandler handler)
    {
        if (_handlerByType.ContainsKey(handler.MessageType))
        {
            throw new InvalidOperationException(
                $"Handler for message type {handler.MessageType} already exists.");
        }

        _handlerByType.Add(handler.MessageType, handler);
    }

    public IMessageHandler Add<T>(Action<T, MessageEnvelope> action)
        where T : IMessage
    {
        var messageHandler = new RelayMessageHandler<T>(action);
        Add(messageHandler);
        return messageHandler;
    }

    public IMessageHandler Add<T>(Action<T> action)
        where T : IMessage
    {
        var messageHandler = new RelayMessageHandler<T>(action);
        Add(messageHandler);
        return messageHandler;
    }

    public void AddRange(IEnumerable<IMessageHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            Add(handler);
        }
    }

    public bool Remove(IMessageHandler handler)
    {
        if (!_handlerByType.Remove(handler.MessageType))
        {
            return false;
        }

        return true;
    }

    public void RemoveRange(IEnumerable<IMessageHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            Remove(handler);
        }
    }

    public bool Contains(Type messageType) => _handlerByType.ContainsKey(messageType);

    public bool TryGetHandler(Type messageType, [MaybeNullWhen(false)] out IMessageHandler handler)
        => _handlerByType.TryGetValue(messageType, out handler);

    public void Handle(MessageEnvelope messageEnvelope)
    {
        var message = messageEnvelope.Message;
        var messageType = message.GetType();
        while (messageType is not null && typeof(IMessage).IsAssignableFrom(messageType))
        {
            if (TryGetHandler(messageType, out var handler))
            {
                handler.Handle(messageEnvelope);
                return;
            }

            messageType = messageType.BaseType;
        }

        if (TryGetHandler(typeof(IMessage), out var handler2))
        {
            handler2.Handle(messageEnvelope);
        }
    }

    public IEnumerator<IMessageHandler> GetEnumerator()
    {
        foreach (var handler in _handlerByType.Values)
        {
            yield return handler;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        foreach (var handler in _handlerByType.Values)
        {
            if (handler is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
