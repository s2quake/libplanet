using System.Collections;
using System.Diagnostics;
using Libplanet.Net.Messages;

namespace Libplanet.Net;

public sealed class MessageHandlerCollection(IEnumerable<IMessageHandler> handlers)
    : IEnumerable<IMessageHandler>
{
    private readonly Dictionary<Type, List<IMessageHandler>> _handlersByType = CreateHandlersByType(handlers);
    private readonly List<IMessageHandler> _handlerList = [.. handlers];

    public MessageHandlerCollection()
        : this([])
    {
    }

    public int Count => _handlersByType.Count;

    public IMessageHandler this[int index] => _handlerList[index];

    public void Add(IMessageHandler handler)
    {
        if (!_handlersByType.TryGetValue(handler.MessageType, out var handlers1))
        {
            handlers1 = [];
        }

        handlers1.Add(handler);
        _handlersByType[handler.MessageType] = handlers1;
        _handlerList.Add(handler);
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
        if (!_handlersByType.TryGetValue(handler.MessageType, out var value))
        {
            return false;
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

        return removed;
    }

    public void RemoveRange(IEnumerable<IMessageHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            Remove(handler);
        }
    }

    public bool Contains(Type messageType) => _handlersByType.ContainsKey(messageType);

    public void Handle(MessageEnvelope messageEnvelope)
    {
        var message = messageEnvelope.Message;
        var messageType = message.GetType();
        while (messageType is not null && typeof(IMessage).IsAssignableFrom(messageType))
        {
            if (_handlersByType.TryGetValue(messageType, out var handlers1))
            {
                foreach (var handler in handlers1)
                {
                    handler.Handle(messageEnvelope);
                }
            }

            messageType = messageType.BaseType;
        }

        if (_handlersByType.TryGetValue(typeof(IMessage), out var handlers2))
        {
            foreach (var handler in handlers2)
            {
                handler.Handle(messageEnvelope);
            }
        }
    }

    public IEnumerator<IMessageHandler> GetEnumerator() => _handlerList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static Dictionary<Type, List<IMessageHandler>> CreateHandlersByType(IEnumerable<IMessageHandler> handlers)
    {
        var query = from handler in handlers
                    group handler by handler.MessageType into @group
                    select new { Type = @group.Key, Handlers = @group.ToList() };
        return query.ToDictionary(item => item.Type, item => item.Handlers);
    }
}
