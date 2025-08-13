using Libplanet.Net.Messages;

namespace Libplanet.Net;

public static class IMessageRouterExtensions
{
    public static IDisposable Register<T>(this IMessageRouter @this, Action<T, MessageEnvelope> action)
        where T : IMessage
        => @this.Register(new RelayMessageHandler<T>((m, e) => action(m, e)));

    public static IDisposable Register<T>(this IMessageRouter @this, Action<T> action)
        where T : IMessage
        => @this.Register(new RelayMessageHandler<T>((m, _) => action(m)));

    public static IDisposable Register<T>(this IMessageRouter @this, Func<T, MessageEnvelope, CancellationToken, Task> func)
        where T : IMessage
        => @this.Register(new RelayMessageAsyncHandler<T>(func));

    public static IDisposable Register<T>(this IMessageRouter @this, Func<T, CancellationToken, Task> func)
        where T : IMessage
        => @this.Register(new RelayMessageAsyncHandler<T>(func));
}
