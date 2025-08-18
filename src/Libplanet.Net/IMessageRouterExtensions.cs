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

    public static IDisposable RegisterMany(this IMessageRouter @this, ImmutableArray<IMessageHandler> handlers)
    {
        var disposerList = new List<IDisposable>(handlers.Length);
        foreach (var handler in handlers)
        {
            disposerList.Add(@this.Register(handler));
        }

        return new DisposerCollection(disposerList);
    }

    public static IDisposable RegisterSendingMessageValidation<T>(this IMessageRouter @this, Action<T> action)
        where T : IMessage
        => @this.Register(new RelaySendingMessageValidator<T>(action));

    public static IDisposable RegisterSendingMessageValidation<T>(
        this IMessageRouter @this, Action<T, MessageEnvelope> action)
        where T : IMessage
        => @this.Register(new RelaySendingMessageValidator<T>(action));

    public static IDisposable RegisterReceivedMessageValidation<T>(this IMessageRouter @this, Action<T> action)
        where T : IMessage
        => @this.Register(new RelayReceivedMessageValidator<T>(action));

    public static IDisposable RegisterReceivedMessageValidation<T>(
        this IMessageRouter @this, Action<T, MessageEnvelope> action)
        where T : IMessage
        => @this.Register(new RelayReceivedMessageValidator<T>(action));
}
