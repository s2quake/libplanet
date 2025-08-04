using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface IMessageRouter
{
    IObservable<(IMessageHandler MessageHandler, Exception Exception)> ErrorOccurred { get; }

    IObservable<MessageEnvelope> InvalidProtocol { get; }

    IDisposable Register(IMessageHandler handler);

    IDisposable Register<T>(Action<T, MessageEnvelope> action)
        where T : IMessage;

    IDisposable Register<T>(Action<T> action)
        where T : IMessage;

    IDisposable RegisterMany(ImmutableArray<IMessageHandler> handlers);
}
