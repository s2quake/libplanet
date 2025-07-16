using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

internal sealed class MessageHandlerCollection(params IMessageHandler[] handlers)
    : IEnumerable<IMessageHandler>, IDisposable
{
    private readonly Dictionary<Type, IMessageHandler> handlers = handlers.ToDictionary(item => item.MessageType);

    public int Count => handlers.Count;

    public IMessageHandler this[Type messageType] => handlers[messageType];

    public async ValueTask HandleAsync(IReplyContext replyContext, CancellationToken cancellationToken)
    {
        if (handlers.TryGetValue(replyContext.Message.GetType(), out var handler))
        {
            await handler.HandleAsync(replyContext, cancellationToken);
        }
    }

    public IEnumerator<IMessageHandler> GetEnumerator()
    {
        foreach (var handler in handlers.Values)
        {
            yield return handler;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        foreach (var handler in handlers.Values)
        {
            if (handler is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
