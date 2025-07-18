namespace Libplanet.Net;

public sealed class MessageHandlerScope(MessageHandlerCollection handlers, IMessageHandler handler)
    : IDisposable
{
    void IDisposable.Dispose() => handlers.Remove(handler);
}
